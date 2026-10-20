using Mastera.Domain.Common;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.Clients;

/// <summary>Someone who books at a salon. Known by phone number, separately in every salon.</summary>
public sealed class Client : Entity<ClientId>, ITenantOwned
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const int NotesMaxLength = 1000;

    private Client(ClientId id, string name, PhoneNumber phone, Instant createdAt)
        : base(id)
    {
        Name = name;
        Phone = phone;
        CreatedAt = createdAt;
    }

    private Client()
        : base(default)
    {
        Name = string.Empty;
        Phone = null!;
    }

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }

    public PhoneNumber Phone { get; private set; }

    public string? Email { get; private set; }

    /// <summary>Private notes for the salon ("allergic to latex", "prefers short cuts").</summary>
    public string? Notes { get; private set; }

    public int NoShowCount { get; private set; }

    public Instant CreatedAt { get; private init; }

    public static Client Create(string? name, PhoneNumber phone, Instant now)
    {
        ArgumentNullException.ThrowIfNull(phone);
        return new Client(ClientId.New(), ValidName(name), phone, now);
    }

    public void Rename(string? name) => Name = ValidName(name);

    public void ChangeEmail(string? email) => Email = Text.Optional(email, 256, "client.email.length", "The email");

    public void ChangeNotes(string? notes) => Notes = Text.Optional(notes, NotesMaxLength, "client.notes.length", "The notes");

    public void RecordNoShow() => NoShowCount++;

    private static string ValidName(string? name) =>
        Text.Required(name, NameMinLength, NameMaxLength, "client.name.length", "The name");
}
