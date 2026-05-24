using Sponsorship.Domain.Common;

namespace Sponsorship.Domain.Entities;

public class SponsorshipType : Entity<int>
{
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private SponsorshipType() { }

    public SponsorshipType(string name)
    {
        Name = name;
        IsActive = true;
    }

    public SponsorshipType(int id, string name) : this(name)
    {
        Id = id;
    }

    public void Rename(string newName) => Name = newName;
    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
