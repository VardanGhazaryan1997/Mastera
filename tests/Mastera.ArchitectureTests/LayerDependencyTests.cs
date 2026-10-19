using System.Reflection;
using NetArchTest.Rules;

namespace Mastera.ArchitectureTests;

/// <summary>
/// Enforces the Clean Architecture dependency rule:
/// Api -> Infrastructure -> Application -> Domain (never the other way round).
/// </summary>
public sealed class LayerDependencyTests
{
    private const string DomainNs = "Mastera.Domain";
    private const string ApplicationNs = "Mastera.Application";
    private const string InfrastructureNs = "Mastera.Infrastructure";
    private const string ApiNs = "Mastera.Api";

    private static readonly Assembly Domain = typeof(Domain.AssemblyMarker).Assembly;
    private static readonly Assembly Application = typeof(Application.AssemblyMarker).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.AssemblyMarker).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer()
    {
        AssertNoDependency(Domain, ApplicationNs, InfrastructureNs, ApiNs);
    }

    [Fact]
    public void Domain_does_not_depend_on_EF_Core_or_ASP_NET()
    {
        AssertNoDependency(Domain, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql");
    }

    [Fact]
    public void Application_does_not_depend_on_Infrastructure_or_Api()
    {
        AssertNoDependency(Application, InfrastructureNs, ApiNs);
    }

    [Fact]
    public void Application_does_not_depend_on_EF_Core_or_ASP_NET()
    {
        AssertNoDependency(Application, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        AssertNoDependency(Infrastructure, ApiNs);
    }

    [Fact]
    public void Every_layer_assembly_is_loaded()
    {
        Domain.GetName().Name.ShouldBe(DomainNs);
        Application.GetName().Name.ShouldBe(ApplicationNs);
        Infrastructure.GetName().Name.ShouldBe(InfrastructureNs);
        typeof(Program).Assembly.GetName().Name.ShouldBe(ApiNs);
    }

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        var offenders = string.Join(", ", result.FailingTypeNames ?? []);
        result.IsSuccessful.ShouldBeTrue($"{assembly.GetName().Name} must not depend on [{string.Join(", ", forbidden)}]. Offending types: {offenders}");
    }
}
