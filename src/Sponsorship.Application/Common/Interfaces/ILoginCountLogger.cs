namespace Sponsorship.Application.Common.Interfaces;

/// <summary>
/// Port for recording successful logins to a day-wise plain-text log so the
/// number of logins per day can be reviewed later. Implementations must never
/// throw — a logging failure must not break the login flow.
/// </summary>
public interface ILoginCountLogger
{
    Task RecordSuccessfulLoginAsync(string userEmail, CancellationToken ct = default);
}
