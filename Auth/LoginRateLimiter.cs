using System.Collections.Concurrent;

namespace GameBackend.Api.Auth;

public class LoginRateLimiter
{
    private class AttemptEntry
    {
        public DateTime WindowStart { get; set; }
        public int Count { get; set; }
    }

    private readonly ConcurrentDictionary<string, AttemptEntry> _attempts = new();
    private readonly int _maxAttempts;
    private readonly TimeSpan _window;

    public LoginRateLimiter(int maxAttempts = 5, TimeSpan? window = null)
    {
        _maxAttempts = maxAttempts;
        _window = window ?? TimeSpan.FromMinutes(1);
    }

    public bool AllowAttempt(string key, out TimeSpan retryAfter)
    {
        var now = DateTime.UtcNow;
        retryAfter = TimeSpan.Zero;

        var entry = _attempts.AddOrUpdate(
            key,
            _ => new AttemptEntry { Count = 1, WindowStart = now },
            (_, existing) =>
            {
                if (now - existing.WindowStart > _window)
                {
                    existing.WindowStart = now;
                    existing.Count = 1;
                }
                else
                {
                    existing.Count += 1;
                }

                return existing;
            });

        if (entry.Count > _maxAttempts)
        {
            retryAfter = (entry.WindowStart + _window) - now;
            return false;
        }

        return true;
    }
}
