using backend.tests.Support;
using FluentAssertions;

namespace backend.tests.Api;

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

    private static string Normalize(string json) => json.ReplaceLineEndings("\n").TrimEnd() + "\n";

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
