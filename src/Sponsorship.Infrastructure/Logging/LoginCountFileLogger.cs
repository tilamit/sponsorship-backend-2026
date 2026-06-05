using Microsoft.Extensions.Logging;
using Sponsorship.Application.Common.Interfaces;

namespace Sponsorship.Infrastructure.Logging;

/// <summary>
/// Writes one day-wise plain-text file per calendar day recording every
/// successful login. Files are named <c>count-login-MM-dd-yyyy.txt</c> (e.g.
/// <c>count-login-06-05-2026.txt</c>) and live under <see cref="_directory"/>.
///
/// Each successful login appends a line that carries the running count for the
/// day, so the last line of any file tells you how many logins happened that
/// day at a glance:
/// <code>
/// [2026-06-05 14:22:10 UTC] Login #3 - admin@demo.local
/// </code>
///
/// Registered as a singleton so the write lock is shared process-wide. All
/// timestamps and the day boundary use UTC, matching the rest of the app.
/// </summary>
public sealed class LoginCountFileLogger : ILoginCountLogger
{
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<LoginCountFileLogger> _logger;
    private readonly string _directory;

    // Serializes file access so concurrent logins don't interleave writes or
    // miscount. Fine for assessment-scale traffic on a single instance.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LoginCountFileLogger(
        IDateTimeProvider clock,
        ILogger<LoginCountFileLogger> logger,
        string directory)
    {
        _clock = clock;
        _logger = logger;
        _directory = directory;
    }

    public async Task RecordSuccessfulLoginAsync(string userEmail, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var fileName = $"count-login-{now:MM-dd-yyyy}.txt";
        var path = Path.Combine(_directory, fileName);

        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_directory);

            // The running count for the day is simply the number of lines
            // already written (one line == one successful login).
            var count = File.Exists(path)
                ? File.ReadLines(path).Count(line => !string.IsNullOrWhiteSpace(line)) + 1
                : 1;

            var line = $"[{now:yyyy-MM-dd HH:mm:ss} UTC] Login #{count} - {userEmail}{Environment.NewLine}";
            await File.AppendAllTextAsync(path, line, ct);
        }
        catch (Exception ex)
        {
            // Never let a logging failure break the login itself.
            _logger.LogWarning(ex, "Failed to write login count log for {Email}", userEmail);
        }
        finally
        {
            _gate.Release();
        }
    }
}
