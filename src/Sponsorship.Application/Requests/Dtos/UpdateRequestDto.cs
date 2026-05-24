namespace Sponsorship.Application.Requests.Dtos;

public record UpdateRequestDto(
    string Title,
    string Department,
    int SponsorshipTypeId,
    string EventName,
    DateTime EventDate,
    decimal RequestedAmount,
    string Purpose,
    string? ExpectedBenefit,
    string? Remarks);
