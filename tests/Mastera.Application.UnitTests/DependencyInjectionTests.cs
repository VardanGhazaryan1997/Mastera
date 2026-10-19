using Mastera.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Mastera.Application.UnitTests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        var result = services.AddApplication();

        result.ShouldBeSameAs(services);
    }
}
