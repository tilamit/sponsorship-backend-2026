namespace Sponsorship.Domain.Enums;

public enum RequestStatus
{
    Draft = 0,
    PendingManagerApproval = 1,
    PendingFinanceReview = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5
}
