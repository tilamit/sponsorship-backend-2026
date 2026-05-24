using FluentValidation;
using Sponsorship.Application.Requests.Dtos;

namespace Sponsorship.Application.Requests.Validators;

public class ApprovalActionDtoValidator : AbstractValidator<ApprovalActionDto>
{
    public ApprovalActionDtoValidator()
    {
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}
