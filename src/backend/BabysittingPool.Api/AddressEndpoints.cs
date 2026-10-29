using BabysittingPool.Application;
using BabysittingPool.Application.Addresses;

namespace BabysittingPool.Api;

internal static class AddressEndpoints
{
    public const string Path = "/api/address-suggestions";

    public static void MapAddressEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet(Path, async (string? q, SuggestAddressesHandler handler, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await handler.HandleAsync(q, cancellationToken));
            }
            catch (AddressQueryTooShortException)
            {
                return Problems.InvalidRequest("q", "too_short");
            }
            catch (AddressServiceUnavailableException)
            {
                return Problems.AddressServiceUnavailable();
            }
        })
        .RequireAuthorization()
        .WithName("SuggestAddresses")
        .Produces<Page<AddressSuggestion>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
