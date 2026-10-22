using System.Text.Json.Serialization;
using BabysittingPool.Api;
using BabysittingPool.Application;
using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Identity;
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
if (builder.Environment.IsEnvironment(TestEndpoints.Environment))
{
    builder.Services.AddTestClock();
}
else
{
    builder.Services.AddSingleton(TimeProvider.System);
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Default") ?? "Data Source=babysitting.db");

// Where links in emails point (the SPA's origin). Required in Production; elsewhere the Vite dev server.
var baseUrl = builder.Configuration["App:BaseUrl"];
if (string.IsNullOrWhiteSpace(baseUrl))
{
    baseUrl = builder.Environment.IsProduction()
        ? throw new InvalidOperationException("App:BaseUrl must be set in Production.")
        : "http://localhost:5173";
}

builder.Services.AddSingleton(new AppLinks(new Uri(baseUrl)));
if (builder.Environment.IsProduction())
{
    builder.Services.AddUnconfiguredEmailSender();
}
else
{
    builder.Services.AddEmailOutbox();
}

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

var adminEmailSetting = app.Configuration["Bootstrap:AdminEmail"];
EmailAddress? adminEmail = null;
if (!string.IsNullOrWhiteSpace(adminEmailSetting))
{
    adminEmail = EmailAddress.TryParse(adminEmailSetting, out var parsed)
        ? parsed
        : throw new InvalidOperationException("Bootstrap:AdminEmail is not a valid email address.");
}

if (app.Environment.IsDevelopment())
{
    await app.Services.SeedDevelopmentAsync(adminEmail?.Value ?? "admin@example.test");
}

// Anywhere but Production an empty database is fine (tests and E2E create their own Members).
if (adminEmail is not null || app.Environment.IsProduction())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<BootstrapAdminHandler>().HandleAsync(adminEmail, CancellationToken.None);
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("E2E"))
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCsrfHeaderCheck();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapAccountEndpoints();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment(TestEndpoints.Environment))
{
    app.MapDevEndpoints();
}

if (app.Environment.IsEnvironment(TestEndpoints.Environment))
{
    app.MapTestEndpoints();
}

app.Run();

public partial class Program;
