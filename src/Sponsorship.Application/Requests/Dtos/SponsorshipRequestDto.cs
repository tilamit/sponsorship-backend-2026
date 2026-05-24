namespace Sponsorship.Application.Requests.Dtos;

public record SponsorshipRequestDto(
    Guid Id,
    string Title,
    Guid RequestorId,
    string RequestorName,
    string Department,
    int SponsorshipTypeId,
    string SponsorshipTypeName,
    string EventName,
    DateTime EventDate,
    decimal RequestedAmount,
    string Purpose,
    string? ExpectedBenefit,
    string? Remarks,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
