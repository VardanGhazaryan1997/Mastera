namespace Mastera.Domain.Invitations;

public readonly record struct InvitationId(Guid Value)
{
    public static InvitationId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
