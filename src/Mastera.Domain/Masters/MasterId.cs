namespace Mastera.Domain.Masters;

public readonly record struct MasterId(Guid Value)
{
    public static MasterId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
