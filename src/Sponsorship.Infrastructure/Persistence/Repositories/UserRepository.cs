using Microsoft.EntityFrameworkCore;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Domain.Entities;

namespace Sponsorship.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id, ct);
}
