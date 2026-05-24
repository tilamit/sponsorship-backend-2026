using Sponsorship.Application.Common.Interfaces;

namespace Sponsorship.Infrastructure.Common;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
