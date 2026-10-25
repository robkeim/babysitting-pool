using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using backend.tests.Support;

namespace backend.tests.Api;

public class RulesEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Without_a_session_the_rules_answer_401()
    {
        using var client = _factory.CreateSpaClient();

        var response = await client.GetAsync("/api/rules");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task The_rules_carry_the_engines_settings()
    {
        using var client = _factory.CreateMemberClient(await _factory.SeedMemberAsync());

        var rules = await client.GetFromJsonAsync<JsonElement>("/api/rules");

        rules.GetProperty("startingBalanceQuarterCredits").GetInt64().Should().Be(40);
        rules.GetProperty("durationIncrementMinutes").GetInt32().Should().Be(15);
        rules.GetProperty("minimumLeadTimeMinutes").GetInt32().Should().Be(6 * 60);
        rules.GetProperty("claimCutoffMinutes").GetInt32().Should().Be(3 * 60);
        rules.GetProperty("demurrageDay").GetString().Should().Be("Monday");
        rules.GetProperty("demurrageTime").GetString().Should().Be("00:00");
        rules.GetProperty("demurrageGraceDays").GetInt32().Should().Be(21);
        rules.GetProperty("demurrageChargeQuarterCredits").GetInt64().Should().Be(8);
        rules.GetProperty("dormancyDays").GetInt32().Should().Be(42);
        rules.GetProperty("prices").GetProperty("secondChildHourlyQuarterCredits").GetInt64().Should().Be(2);
    }

    [Fact]
    public async Task Every_example_is_priced_by_the_domain_calculator()
    {
        using var client = _factory.CreateMemberClient(await _factory.SeedMemberAsync());

        var rules = await client.GetFromJsonAsync<JsonElement>("/api/rules");

        var examples = rules.GetProperty("examples").EnumerateArray().ToList();
        examples.Should().NotBeEmpty();
        foreach (var example in examples)
        {
            var meals =
                (example.GetProperty("lunch").GetBoolean() ? MealOption.Lunch : MealOption.None) |
                (example.GetProperty("snack").GetBoolean() ? MealOption.Snack : MealOption.None) |
                (example.GetProperty("dinner").GetBoolean() ? MealOption.Dinner : MealOption.None);
            var details = new CreditRequestDetails(
                example.GetProperty("durationMinutes").GetInt32(),
                example.GetProperty("childCount").GetInt32(),
                Enum.Parse<BabysittingLocation>(example.GetProperty("location").GetString()!),
                Enum.Parse<TransportOption>(example.GetProperty("transport").GetString()!),
                meals);

            var expected = Credits.FromDecimal(CreditCostCalculator.Calculate(details).Total).QuarterCredits;

            example.GetProperty("quote").GetProperty("costQuarterCredits").GetInt64().Should().Be(expected);
        }
    }
}
