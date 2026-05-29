using Sponsorship.Application.Common.Interfaces;

namespace Sponsorship.Application.Tests.TestSupport;

/// A deterministic clock so tests never depend on the wall clock.
public sealed class FixedDateTimeProvider : IDateTimeProvider
{
    public FixedDateTimeProvider(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; set; }

    /// A stable, arbitrary instant used by most tests.
    public static FixedDateTimeProvider Default =>
        new(new DateTime(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc));
}
