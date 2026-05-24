using Sponsorship.Domain.Enums;

namespace Sponsorship.Domain.Exceptions;

public class InvalidWorkflowTransitionException : DomainException
{
    public InvalidWorkflowTransitionException(RequestStatus from, WorkflowAction action)
        : base($"Cannot perform action '{action}' on a request in status '{from}'.") { }
}
