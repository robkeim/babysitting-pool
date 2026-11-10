using System.Text.Json.Serialization;
using BabysittingPool.Application;
using BabysittingPool.Application.Addresses;
using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Identity;
using BabysittingPool.Infrastructure;

namespace BabysittingPool.Api;

/// <summary>What a host other than the plain API adds to it. The E2E host is the only one (ADR 0004, ADR 0016).</summary>
public sealed class ApiHostOptions
{
    /// <summary>Forces the environment name, whatever <c>ASPNETCORE_ENVIRONMENT</c> says.</summary>
    public string? EnvironmentName { get; init; }

    /// <summary>
    /// Where <c>appsettings.json</c> is read from. The E2E host's own folder has none; the API's is copied next to it.
    /// </summary>
    public string? ContentRootPath { get; init; }

    /// <summary>Serve the session cookie without <c>Secure</c> (Safari drops it on <c>http://localhost</c>).</summary>
    public bool PlainHttpCookies { get; init; }

    /// <summary>Serve the OpenAPI document outside Development.</summary>
    public bool ServeOpenApi { get; init; }

    /// <summary>Runs first, before the API reads any configuration.</summary>
    public Action<WebApplicationBuilder>? ConfigureBuilder { get; init; }

    /// <summary>Runs after the API has registered its services, so it can replace them.</summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }

    /// <summary>Runs after the API has mapped its endpoints, before the SPA fallback.</summary>
    public Action<IEndpointRouteBuilder>? MapEndpoints { get; init; }
}

/// <summary>Builds the API. <c>Program</c> runs it as is; the E2E host adds its test-only parts through the options.</summary>
public static class ApiHost
{
    public static async Task<WebApplication> BuildAsync(string[] args, ApiHostOptions? hostOptions = null)
    {
        var host = hostOptions ?? new ApiHostOptions();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = host.EnvironmentName,
            ContentRootPath = host.ContentRootPath,
            // Not the entry assembly's name, which would reach the OpenAPI document and differ per host.
            ApplicationName = typeof(ApiHost).Assembly.GetName().Name,
        });
        ProductionGuard.Refuse(builder);
        host.ConfigureBuilder?.Invoke(builder);

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
        {
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            // Enums travel as their names, so the contract says "Posted" rather than 0.
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        builder.Services.AddSingleton(new SessionCookieSettings(PlainHttp: host.PlainHttpCookies));

        // Where links in emails point (the SPA's origin). Required in Production; elsewhere the Vite dev server.
        var baseUrl = builder.Configuration["App:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = builder.Environment.IsProduction()
                ? throw new InvalidOperationException("App:BaseUrl must be set in Production.")
                : "http://localhost:5173";
        }

        // SMTP secrets (dotnet user-secrets, ADR 0011) are only ever honored in Development and Production; E2E and
        // tests always use the fake outbox, even on a machine that has them.
        var smtp = builder.Configuration.SmtpSettings();
        Action<IServiceCollection> addEmailSender;
        if (builder.Environment.IsProduction())
        {
            // Production cannot invite anyone without email (ADR 0018), so refuse to start rather than fail on first send.
            var productionSmtp = smtp
                ?? throw new InvalidOperationException("Email:Smtp:Address and Email:Smtp:Password must be set in Production.");
            addEmailSender = services => services.AddSmtpEmailSender(productionSmtp);
        }
        else if (builder.Environment.IsDevelopment() && smtp is not null)
        {
            addEmailSender = services => services.AddEmailOutboxOverSmtp(smtp);
        }
        else
        {
            addEmailSender = services => services.AddEmailOutbox();
        }

        builder.Services.AddCoopCore(new CoopCoreOptions(
            builder.Configuration.GetConnectionString("Default") ?? "Data Source=babysitting.db",
            new Uri(baseUrl),
            addEmailSender));

        builder.Services.AddHttpClient<IAddressSuggester, BanAddressSuggester>(client =>
        {
            client.BaseAddress = new Uri(BanAddressSuggester.BaseAddress);
            client.Timeout = BanAddressSuggester.Timeout;
        })
        // The default handlers log the full request URL, which holds the query: a partial home address (ADR 0010).
        .RemoveAllLoggers();
        builder.Services.AddExceptionHandler<DomainFailureHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddSessionAuthentication();
        builder.Services.AddFeatureFlags();
        builder.Services.AddApiRateLimiting(builder.Configuration);

        if (builder.Configuration.GetValue(SettleHostedService.EnabledSetting, true))
        {
            builder.Services.AddHostedService<SettleHostedService>();
        }

        builder.Services.AddHostedService<EmailDeliveryService>();

        host.ConfigureServices?.Invoke(builder.Services);

        var app = builder.Build();

        await app.Services.MigrateDatabaseAsync();

        await app.Services.SeedFeatureFlagsAsync(app.Configuration);

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
            await app.Services.SeedDevelopmentAsync(adminEmail?.Value, smtp?.Address);
        }

        // Anywhere but Production an empty database is fine (tests and E2E create their own Members).
        if (adminEmail is not null || app.Environment.IsProduction())
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<BootstrapAdminHandler>().HandleAsync(adminEmail, CancellationToken.None);
        }

        if (app.Environment.IsDevelopment() || host.ServeOpenApi)
        {
            app.MapOpenApi();
        }

        app.UseExceptionHandler();
        app.UseConfiguredForwardedHeaders();
        if (app.Environment.IsProduction())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseSecurityHeaders();
        app.UseRequestSizeLimit();
        var servesSpa = app.Environment.HasSpa();
        if (servesSpa)
        {
            app.UseSpaStaticFiles();
        }

        app.UseCsrfHeaderCheck();
        app.UseAuthentication();
        app.UseApiRateLimiting();
        app.UseAuthorization();

        app.MapGet("/health", HealthCheck.HandleAsync);
        app.MapAuthEndpoints();
        app.MapAccountEndpoints();
        app.MapRequestEndpoints();
        app.MapNotificationEndpoints();
        app.MapAddressEndpoints();
        app.MapAdminEndpoints();
        app.MapFeatureFlagEndpoints();

        if (app.Environment.IsDevelopment())
        {
            app.MapDevEndpoints();
        }

        host.MapEndpoints?.Invoke(app);

        if (servesSpa)
        {
            app.MapSpaFallback();
        }

        return app;
    }
}
