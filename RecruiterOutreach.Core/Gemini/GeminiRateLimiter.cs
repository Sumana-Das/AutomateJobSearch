using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RecruiterOutreach.Core.Gemini;

/// <summary>
/// Utility responsible for enforcing simple in-memory RPM/RPD rate limits for Gemini calls.
/// </summary>
public static class GeminiRateLimiter
{
    public static async Task ThrottleAsync(
        int rpmLimit,
        int rpdLimit,
        object rateLock,
        Queue<DateTime> minuteWindow,
        Queue<DateTime> dayWindow,
        CancellationToken cancellationToken)
    {
        if (rpmLimit <= 0 && rpdLimit <= 0)
        {
            return;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTime.UtcNow;
            TimeSpan? delay = null;

            lock (rateLock)
            {
                // Clean old entries from the 1-minute window
                while (minuteWindow.Count > 0 && (now - minuteWindow.Peek()).TotalSeconds >= 60)
                {
                    minuteWindow.Dequeue();
                }

                // Clean old entries from the 1-day window
                while (dayWindow.Count > 0 && (now - dayWindow.Peek()).TotalDays >= 1)
                {
                    dayWindow.Dequeue();
                }

                if (rpdLimit > 0 && dayWindow.Count >= rpdLimit)
                {
                    throw new InvalidOperationException("Gemini daily rate limit reached for this application. Please try again tomorrow or upgrade your plan.");
                }

                if (rpmLimit > 0 && minuteWindow.Count >= rpmLimit)
                {
                    var oldest = minuteWindow.Peek();
                    var waitUntil = oldest.AddMinutes(1);
                    var toWait = waitUntil - now;
                    if (toWait < TimeSpan.Zero)
                    {
                        toWait = TimeSpan.Zero;
                    }

                    delay = toWait;
                }
                else
                {
                    minuteWindow.Enqueue(now);
                    dayWindow.Enqueue(now);
                    return;
                }
            }

            if (delay.HasValue && delay.Value > TimeSpan.Zero)
            {
                await Task.Delay(delay.Value, cancellationToken);
            }
            else
            {
                // Loop again to re-check windows
            }
        }
    }
}
