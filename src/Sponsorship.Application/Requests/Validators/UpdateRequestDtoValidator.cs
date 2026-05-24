using FluentValidation;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Application.Requests.Dtos;

namespace Sponsorship.Application.Requests.Validators;

public class UpdateRequestDtoValidator : AbstractValidator<UpdateRequestDto>
{
    public UpdateRequestDtoValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SponsorshipTypeId).GreaterThan(0);
        RuleFor(x => x.EventName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EventDate)
            .GreaterThanOrEqualTo(clock.UtcNow.Date)
            .WithMessage("Event date must be today or later.");
        RuleFor(x => x.RequestedAmount).GreaterThan(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ExpectedBenefit).MaximumLength(1000);
        RuleFor(x => x.Remarks).MaximumLength(500);
    }
}
