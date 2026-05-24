using FluentValidation;
using Sponsorship.Application.Requests.Dtos;

namespace Sponsorship.Application.Requests.Validators;

public class CreateSponsorshipTypeDtoValidator : AbstractValidator<CreateSponsorshipTypeDto>
{
    public CreateSponsorshipTypeDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class UpdateSponsorshipTypeDtoValidator : AbstractValidator<UpdateSponsorshipTypeDto>
{
    public UpdateSponsorshipTypeDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
