using Mastera.Application.Common;
using Mastera.Application.Memberships;
using Mastera.Application.Salons;
using Mastera.Application.Users;
using Mastera.Domain.Memberships;
using Mastera.Domain.Tenants;
using NodaTime;
using NSubstitute;

// NSubstitute argument specs (CreateAsync(default!, ..., default)) are not real calls, so xUnit1051 doesn't apply.
#pragma warning disable xUnit1051

namespace Mastera.Application.UnitTests.Salons;

public sealed class RegisterSalonHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 20, 10, 0);

    private readonly FakeTenants _tenants = new();
    private readonly FakeMemberships _memberships = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly IUserAccounts _users = Substitute.For<IUserAccounts>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Guid _ownerId = Guid.NewGuid();

    public RegisterSalonHandlerTests()
    {
        _clock.GetCurrentInstant().Returns(Now);
        _users.CreateAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(Result.Success(_ownerId));
    }

    private RegisterSalonHandler Handler => new(_tenants, _memberships, _users, _unitOfWork, _clock);

    private static RegisterSalonCommand ValidCommand(string slug = "barber-house") =>
        new("Barber House", slug, "Giorgi Beridze", "giorgi@example.ge", "secret123");

    [Fact]
    public async Task Creates_the_salon_the_owner_account_and_the_owner_membership()
    {
        var result = await Handler.HandleAsync(ValidCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var tenant = _tenants.Added.ShouldHaveSingleItem();
        tenant.Name.ShouldBe("Barber House");
        tenant.Slug.Value.ShouldBe("barber-house");
        tenant.CreatedAt.ShouldBe(Now);

        var membership = _memberships.Added.ShouldHaveSingleItem();
        membership.TenantId.ShouldBe(tenant.Id);
        membership.UserId.ShouldBe(_ownerId);
        membership.Role.ShouldBe(MembershipRole.Owner);

        result.Value!.TenantId.ShouldBe(tenant.Id);
        result.Value.OwnerUserId.ShouldBe(_ownerId);
        _unitOfWork.Saves.ShouldBe(1);
        _unitOfWork.Committed.ShouldBeTrue();
        await _users.Received(1).CreateAsync("giorgi@example.ge", "secret123", "Giorgi Beridze", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Uses_Georgian_defaults_when_regional_settings_are_not_given()
    {
        await Handler.HandleAsync(ValidCommand(), CancellationToken.None);

        var tenant = _tenants.Added.ShouldHaveSingleItem();
        tenant.TimeZone.ShouldBe(TimeZoneId.Tbilisi);
        tenant.Currency.ShouldBe(Currency.Gel);
        tenant.DefaultLanguage.ShouldBe(Language.Georgian);
    }

    [Fact]
    public async Task Uses_the_regional_settings_given()
    {
        var command = ValidCommand() with { TimeZone = "Asia/Yerevan", Currency = "AMD", Language = "ru" };

        await Handler.HandleAsync(command, CancellationToken.None);

        var tenant = _tenants.Added.ShouldHaveSingleItem();
        tenant.TimeZone.Value.ShouldBe("Asia/Yerevan");
        tenant.Currency.Code.ShouldBe("AMD");
        tenant.DefaultLanguage.ShouldBe(Language.Russian);
    }

    [Theory]
    [InlineData("bad--slug", "tenant.slug.format")]
    [InlineData("ab", "tenant.slug.length")]
    [InlineData("admin", "tenant.slug.reserved")]
    public async Task Rejects_an_invalid_address_before_creating_anything(string slug, string code)
    {
        var result = await Handler.HandleAsync(ValidCommand(slug), CancellationToken.None);

        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(code);
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        await _users.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default!, default);
        _tenants.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejects_an_invalid_salon_name()
    {
        var result = await Handler.HandleAsync(ValidCommand() with { SalonName = "B" }, CancellationToken.None);

        result.Error!.Code.ShouldBe("tenant.name.length");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("G")]
    public async Task Rejects_a_missing_or_too_short_owner_name(string? ownerName)
    {
        var result = await Handler.HandleAsync(ValidCommand() with { OwnerName = ownerName }, CancellationToken.None);

        result.Error!.Code.ShouldBe("user.name.length");
        _tenants.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejects_an_address_that_is_already_taken()
    {
        _tenants.ExistingSlugs.Add("barber-house");

        var result = await Handler.HandleAsync(ValidCommand(), CancellationToken.None);

        result.Error!.Code.ShouldBe("tenant.slug.taken");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
        await _users.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Passes_on_account_errors_and_rolls_back()
    {
        _users.CreateAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(Result.Failure<Guid>(Error.Conflict("auth.email.taken", "taken")));

        var result = await Handler.HandleAsync(ValidCommand(), CancellationToken.None);

        result.Error!.Code.ShouldBe("auth.email.taken");
        _tenants.Added.ShouldBeEmpty();
        _memberships.Added.ShouldBeEmpty();
        _unitOfWork.Committed.ShouldBeFalse();
        _unitOfWork.Saves.ShouldBe(0);
    }

    private sealed class FakeTenants : ITenantRepository
    {
        public List<Tenant> Added { get; } = [];

        public HashSet<string> ExistingSlugs { get; } = [];

        public Task<bool> SlugExistsAsync(TenantSlug slug, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingSlugs.Contains(slug.Value));

        public Task<Tenant?> FindAsync(TenantId id, CancellationToken cancellationToken) =>
            Task.FromResult(Added.FirstOrDefault(t => t.Id == id));

        public void Add(Tenant tenant) => Added.Add(tenant);
    }

    private sealed class FakeMemberships : IMembershipRepository
    {
        public List<Membership> Added { get; } = [];

        public void Add(Membership membership) => Added.Add(membership);

        public Task<IReadOnlyList<MembershipView>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MembershipView?> FindAsync(Guid userId, TenantSlug slug, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MemberView>> ListMembersAsync(TenantId tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public bool Committed { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.CompletedTask;
        }

        public async Task<Result<T>> InTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken)
        {
            var result = await work(cancellationToken);
            Committed = result.IsSuccess;
            return result;
        }
    }
}
