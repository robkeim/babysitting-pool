using BabysittingPool.Application.Credits;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetMyAccountHandler>();
        return services;
    }
}
