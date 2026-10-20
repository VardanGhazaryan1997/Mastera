using Mastera.Domain.Common;

namespace Mastera.Application.Common;

public static class Errors
{
    public static Error FromDomain(DomainException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Error.Validation(exception.Code, exception.Message);
    }

    public static Error NoTenant() =>
        new("tenant.none", "Choose a salon first.", ErrorKind.Forbidden);
}
