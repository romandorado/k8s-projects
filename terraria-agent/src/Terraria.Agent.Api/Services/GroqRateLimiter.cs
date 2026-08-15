namespace Terraria.Agent.Api.Services;

public class GroqRateLimiter
{
    private readonly int _maxPerMinute;
    private readonly Queue<DateTime> _callTimes = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<GroqRateLimiter> _logger;

    public GroqRateLimiter(IConfiguration config, ILogger<GroqRateLimiter> logger)
    {
        _logger = logger;
        _maxPerMinute = Math.Max(1, config.GetValue("Groq:MaxRequestsPerMinute", 28));
    }

    public async Task WaitForSlotAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            while (_callTimes.Count > 0 && (now - _callTimes.Peek()).TotalSeconds >= 60)
                _callTimes.Dequeue();

            if (_callTimes.Count >= _maxPerMinute)
            {
                var waitMs = (int)Math.Ceiling(60000 - (now - _callTimes.Peek()).TotalMilliseconds);
                if (waitMs > 0)
                {
                    _logger.LogInformation("Groq rate limiter: waiting {Ms}ms for a slot", waitMs);
                    await Task.Delay(waitMs);
                }
                _callTimes.Dequeue();
                _callTimes.Enqueue(DateTime.UtcNow);
                return;
            }

            _callTimes.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            _gate.Release();
        }
    }
}
