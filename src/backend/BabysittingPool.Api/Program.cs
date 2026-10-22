using System.Text.Json.Serialization;
using BabysittingPool.Api;
using BabysittingPool.Application;
using BabysittingPool.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Deterministic output (ADR 0006): the document is committed and drift-checked, so it must not
// vary with the host it was fetched from.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Servers?.Clear();
    return Task.CompletedTask;
}));
// Numbers are numbers (ADR 0001): without this the schema also admits strings, and the generated
// frontend types become `number | string`.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
if (builder.Environment.IsEnvironment(TestEndpoints.Environment)) builder.Services.AddTestClock();
else builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Default") ?? "Data Source=babysitting.db");

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    await app.Services.SeedMemberAsync(DevIdentity.DevMemberId);
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("E2E"))
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAccountEndpoints();

if (app.Environment.IsEnvironment(TestEndpoints.Environment))
{
    app.MapTestEndpoints();
}

app.Run();

public partial class Program;
