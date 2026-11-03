using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Tests.Support;

namespace BabysittingPool.Tests.Api;

public class OpenApiDocumentTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    /// <summary>
    /// The frontend generates its types from the committed file (ADR 0006), so it must match the API.
    /// Regenerate it with <c>UPDATE_OPENAPI=1 dotnet test --filter OpenApiDocumentTests</c>.
    /// </summary>
    [Fact]
    public async Task Committed_openapi_document_matches_the_api()
    {
        using var client = _factory.CreateClient();
        var generated = Normalize(await client.GetStringAsync("/openapi/v1.json"));
        var committedPath = Path.Combine(FindRepositoryRoot(), "docs", "api", "openapi.json");

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(committedPath)!);
            await File.WriteAllTextAsync(committedPath, generated);
        }

        File.Exists(committedPath).Should().BeTrue("docs/api/openapi.json must be committed");
        Normalize(await File.ReadAllTextAsync(committedPath)).Should().Be(generated);
    }

    [Fact]
    public async Task Quarter_credit_fields_are_plain_integers_in_the_schema()
    {
        using var client = _factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        var account = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("AccountView");

        account.GetProperty("properties").GetProperty("availableQuarterCredits")
            .GetProperty("type").GetString().Should().Be("integer");
    }

    private static string Normalize(string json) => json.ReplaceLineEndings("\n").TrimEnd() + "\n";

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
