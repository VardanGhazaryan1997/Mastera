using Mastera.Domain;

namespace Mastera.Domain.UnitTests;

public sealed class DomainAssemblyTests
{
    [Fact]
    public void Domain_assembly_is_named_Mastera_Domain()
    {
        typeof(AssemblyMarker).Assembly.GetName().Name.ShouldBe("Mastera.Domain");
    }
}
