using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Terraria.Agent.Api.Services;
using Xunit;

namespace Terraria.Agent.Api.Tests;

public class GroqRateLimiterTests
{
    private static GroqRateLimiter BuildLimiter(string max)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Groq:MaxRequestsPerMinute"] = max
            })
            .Build();
        return new GroqRateLimiter(config, NullLogger<GroqRateLimiter>.Instance);
    }

    [Fact]
    public async Task RequestsWithinLimitProceedWithoutLongWaits()
    {
        var limiter = BuildLimiter("3");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < 3; i++)
            await limiter.WaitForSlotAsync();

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Expected quick acquisitions, took {stopwatch.Elapsed}");
    }
}
