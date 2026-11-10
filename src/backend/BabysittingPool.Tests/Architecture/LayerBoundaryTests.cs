using System.Reflection;
using AwesomeAssertions;
using BabysittingPool.Api;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Infrastructure;
using NetArchTest.Rules;

namespace BabysittingPool.Tests.Architecture;

/// <summary>
/// Layer boundaries of the hexagonal backend (ADR 0002, ADR 0004). The table in docs/testing.md lists the same rules.
/// </summary>
public class LayerBoundaryTests
{
    private const string ApplicationNamespace = "BabysittingPool.Application";
    private const string InfrastructureNamespace = "BabysittingPool.Infrastructure";
    private const string ApiNamespace = "BabysittingPool.Api";
    private const string TestSupportNamespace = "BabysittingPool.TestSupport";
    private const string E2ENamespace = "BabysittingPool.E2E";

    private static readonly Assembly Domain = typeof(Credits).Assembly;
    private static readonly Assembly Application = typeof(BabysittingPool.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(FeatureFlagServices).Assembly;
    private static readonly Assembly Api = typeof(ApiHost).Assembly;

    private static readonly string[] FrameworkNamespaces =
    [
        "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Microsoft.Data", "Microsoft.Extensions", "System.Data",
        "System.Net.Http", "MailKit", "MimeKit", "Serilog",
    ];

    [Fact]
    public void Domain_depends_on_no_framework_database_or_outer_layer()
    {
        string[] forbidden = [ApplicationNamespace, InfrastructureNamespace, ApiNamespace, TestSupportNamespace, E2ENamespace, .. FrameworkNamespaces];

        AssertNoDependencyOn(Domain, forbidden);
    }

    [Fact]
    public void Application_depends_on_the_domain_only()
    {
        // Microsoft.Extensions.DependencyInjection and Logging abstractions are the only framework references it may keep.
        string[] forbidden =
        [
            InfrastructureNamespace, ApiNamespace, TestSupportNamespace, E2ENamespace, "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore", "Microsoft.Data", "System.Data", "System.Net.Http", "MailKit", "MimeKit", "Serilog",
        ];

        AssertNoDependencyOn(Application, forbidden);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_api_or_test_projects()
    {
        AssertNoDependencyOn(Infrastructure, [ApiNamespace, TestSupportNamespace, E2ENamespace]);
    }

    [Fact]
    public void Api_does_not_depend_on_the_test_only_projects()
    {
        // The Api is the composition root, so it may reference Infrastructure; only the E2E host adds TestSupport.
        AssertNoDependencyOn(Api, [TestSupportNamespace, E2ENamespace]);
    }

    [Fact]
    public void No_handler_references_another_handler()
    {
        var handlerTypes = Types.InAssembly(Application).That().HaveNameEndingWith("Handler", StringComparison.Ordinal).GetTypes().ToList();
        var violations = new List<string>();

        foreach (var handler in handlerTypes)
        {
            var others = handlerTypes.Where(other => other != handler).Select(other => other.FullName!).ToArray();
            var result = Types.InAssembly(Application).That().HaveName(handler.Name).ShouldNot().HaveDependencyOnAny(others).GetResult();
            violations.AddRange(result.FailingTypeNames ?? []);
        }

        violations.Should().BeEmpty("handlers are independent use cases; share logic through a service or the domain (ADR 0002)");
    }

    private static void AssertNoDependencyOn(Assembly assembly, string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        result.IsSuccessful.Should().BeTrue("these types reference a forbidden layer: {0}", string.Join(", ", result.FailingTypeNames ?? []));
    }
}
