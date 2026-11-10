using BabysittingPool.Application.Addresses;
using BabysittingPool.Application.Admin;
using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Flags;
using BabysittingPool.Application.Identity;
using BabysittingPool.Application.Notifications;
using BabysittingPool.Application.Offboarding;
using BabysittingPool.Application.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetMyAccountHandler>();
        services.AddScoped<SuggestAddressesHandler>();
        services.AddSingleton(new CoopSettings());
        services.AddScoped<InvitationMailer>();
        services.AddScoped<GetRulesHandler>();
        services.AddScoped<InvitationIssuer>();
        services.AddScoped<ListInvitationsHandler>();
        services.AddScoped<ListMembersHandler>();
        services.AddScoped<InviteMemberHandler>();
        services.AddScoped<ResendInvitationHandler>();
        services.AddScoped<PreviewInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<RequestSignInLinkHandler>();
        services.AddScoped<RedeemSignInLinkHandler>();
        services.AddScoped<ResolveSessionHandler>();
        services.AddScoped<ChangeLanguageHandler>();
        services.AddScoped<ChangeColorSchemeHandler>();
        services.AddScoped<GetMyProfileHandler>();
        services.AddScoped<UpdateMyProfileHandler>();
        services.AddScoped<EndSessionHandler>();
        services.AddScoped<BootstrapAdminHandler>();
        services.AddScoped<PostRequestHandler>();
        services.AddScoped<ClaimRequestHandler>();
        services.AddScoped<CancelRequestHandler>();
        services.AddScoped<WithdrawClaimHandler>();
        services.AddScoped<EditRequestNotesHandler>();
        services.AddScoped<SettleHandler>();
        services.AddScoped<OffboardMemberHandler>();
        services.AddScoped<EraseMemberHandler>();
        services.AddScoped<GetMemberDetailHandler>();
        services.AddScoped<ListMemberRequestsHandler>();
        services.AddScoped<GetPoolOverviewHandler>();
        services.AddScoped<ListDormantMembersHandler>();
        services.AddScoped<NotificationAnnouncer>();
        services.AddScoped<ListNotificationsHandler>();
        services.AddScoped<DismissNotificationHandler>();
        services.AddScoped<DismissAllNotificationsHandler>();
        services.AddSingleton(FeatureFlagRegistry.Production);
        services.AddScoped<ListFeatureFlagsHandler>();
        services.AddScoped<UpdateFeatureFlagHandler>();
        return services;
    }
}
