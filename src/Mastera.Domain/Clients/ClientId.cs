namespace Mastera.Domain.Clients;

public readonly record struct ClientId(Guid Value)
{
    public static ClientId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
