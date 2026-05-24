namespace Sponsorship.Application.Requests.Dtos;

public record SponsorshipTypeDto(int Id, string Name, bool IsActive);

public record CreateSponsorshipTypeDto(string Name);

public record UpdateSponsorshipTypeDto(string Name, bool IsActive);
