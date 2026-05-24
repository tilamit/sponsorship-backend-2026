namespace Sponsorship.Application.Requests.Dtos;

public record CreateRequestDto(
    string Title,
    string Department,
    int SponsorshipTypeId,
    string EventName,
    DateTime EventDate,
    decimal RequestedAmount,
    string Purpose,
    string? ExpectedBenefit,
    string? Remarks);
