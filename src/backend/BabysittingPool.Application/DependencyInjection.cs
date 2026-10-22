using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Identity;
using BabysittingPool.Application.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetMyAccountHandler>();
        services.AddSingleton(new CoopSettings());
        services.AddScoped<IssueInvitationHandler>();
        services.AddScoped<PreviewInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<RequestSignInLinkHandler>();
        services.AddScoped<RedeemSignInLinkHandler>();
        services.AddScoped<ResolveSessionHandler>();
        services.AddScoped<ChangeLanguageHandler>();
        services.AddScoped<EndSessionHandler>();
        services.AddScoped<BootstrapAdminHandler>();
        services.AddScoped<PostRequestHandler>();
        services.AddScoped<ListMyRequestsHandler>();
        return services;
    }
}
