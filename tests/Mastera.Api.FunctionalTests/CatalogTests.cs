using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mastera.Api.FunctionalTests;

/// <summary>Task 2.1: service categories and services.</summary>
[Trait("Category", "Integration")]
public sealed class CatalogTests(ApiWithDatabaseFactory factory) : IClassFixture<ApiWithDatabaseFactory>
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response, CancellationToken ct) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();

    [Fact]
    public async Task Owner_creates_a_category_and_a_service_in_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await factory.SignUpSalonAsync();

        var category = await client.PostAsJsonAsync("/api/service-categories", new { name = "Haircuts" }, ct);
        category.StatusCode.ShouldBe(HttpStatusCode.Created);
        var categoryId = (await JsonAsync(category, ct)).GetProperty("id").GetGuid();

        var service = await client.PostAsJsonAsync(
            "/api/services",
            new { name = "Men's haircut", durationMinutes = 45, price = 35.50m, categoryId, description = "With wash" },
            ct);

        service.StatusCode.ShouldBe(HttpStatusCode.Created);
        var list = await JsonAsync(await client.GetAsync("/api/services", ct), ct);
        var item = list.EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("name").GetString().ShouldBe("Men's haircut");
        item.GetProperty("durationMinutes").GetInt32().ShouldBe(45);
        item.GetProperty("price").GetDecimal().ShouldBe(35.50m);
        item.GetProperty("categoryId").GetGuid().ShouldBe(categoryId);
    }

    [Fact]
    public async Task Invalid_service_returns_400_with_the_rule_that_failed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await factory.SignUpSalonAsync();

        var response = await client.PostAsJsonAsync("/api/services", new { name = "Haircut", durationMinutes = 7, price = 20 }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync(ct)).ShouldBe("service.duration.invalid");
    }

    [Fact]
    public async Task Service_names_are_unique_per_salon_ignoring_case()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await factory.SignUpSalonAsync();
        await client.PostAsJsonAsync("/api/services", new { name = "Manicure", durationMinutes = 60, price = 40 }, ct);

        var duplicate = await client.PostAsJsonAsync("/api/services", new { name = "MANICURE", durationMinutes = 30, price = 20 }, ct);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ProblemCodeAsync(ct)).ShouldBe("service.name.taken");

        var (otherSalon, _) = await factory.SignUpSalonAsync();
        (await otherSalon.PostAsJsonAsync("/api/services", new { name = "Manicure", durationMinutes = 60, price = 40 }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_archive_and_restore_a_service()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await factory.SignUpSalonAsync();
        var created = await client.PostAsJsonAsync("/api/services", new { name = "Pedicure", durationMinutes = 60, price = 50 }, ct);
        var id = (await JsonAsync(created, ct)).GetProperty("id").GetGuid();

        var updated = await client.PutAsJsonAsync($"/api/services/{id}", new { name = "Spa pedicure", durationMinutes = 75, price = 65 }, ct);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(updated, ct)).GetProperty("name").GetString().ShouldBe("Spa pedicure");

        (await client.PostAsync($"/api/services/{id}/archive", null, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(await client.GetAsync("/api/services", ct), ct)).GetArrayLength().ShouldBe(0);
        (await JsonAsync(await client.GetAsync("/api/services?includeArchived=true", ct), ct)).GetArrayLength().ShouldBe(1);

        (await client.PostAsync($"/api/services/{id}/restore", null, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(await client.GetAsync("/api/services", ct), ct)).GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task A_salon_never_sees_or_changes_another_salons_services()
    {
        var ct = TestContext.Current.CancellationToken;
        var (salonA, _) = await factory.SignUpSalonAsync();
        var (salonB, _) = await factory.SignUpSalonAsync();
        var created = await salonA.PostAsJsonAsync("/api/services", new { name = "Secret service", durationMinutes = 30, price = 10 }, ct);
        var id = (await JsonAsync(created, ct)).GetProperty("id").GetGuid();

        (await JsonAsync(await salonB.GetAsync("/api/services?includeArchived=true", ct), ct)).GetArrayLength().ShouldBe(0);
        (await salonB.PutAsJsonAsync($"/api/services/{id}", new { name = "Hacked", durationMinutes = 30, price = 1 }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await salonB.PostAsync($"/api/services/{id}/archive", null, ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Masters_can_read_the_menu_but_not_change_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = await factory.CreateUserAsync(Mastera.Domain.Memberships.MembershipRole.Master);
        var client = factory.CreatePlatformClient();
        await client.LoginAsync(email, ApiWithDatabaseFactory.Password, ct);
        client.DefaultRequestHeaders.Add("X-Mastera-Tenant", ApiWithDatabaseFactory.SeededSlug);

        (await client.GetAsync("/api/services", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/services", new { name = "Nope", durationMinutes = 30, price = 10 }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
