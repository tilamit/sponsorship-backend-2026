using NSubstitute;
using Sponsorship.Application.Common.Interfaces;

namespace Sponsorship.Application.Tests.TestSupport;

/// Builds an <see cref="IUnitOfWork"/> mock whose <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>
/// simply runs the supplied operation in-line (there is no real database in unit tests).
/// This lets transaction-wrapped service code be asserted exactly like the
/// non-transactional path — the mutation runs and any SaveChangesAsync / exception
/// propagates as it would inside the real serializable transaction.
public static class UnitOfWorkSubstitute
{
    public static IUnitOfWork Create()
    {
        var uow = Substitute.For<IUnitOfWork>();
        uow.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]).Invoke(ci.ArgAt<CancellationToken>(1)));
        return uow;
    }
}
