using Sponsorship.Domain.Common;

namespace Sponsorship.Domain.Entities;

public class Role : Entity<int>
{
    public string Name { get; private set; } = default!;

    public const string Requestor = "Requestor";
    public const string Manager = "Manager";
    public const string FinanceAdmin = "FinanceAdmin";
    public const string SystemAdmin = "SystemAdmin";

    private Role() { }

    public Role(int id, string name)
    {
        Id = id;
        Name = name;
    }
}
