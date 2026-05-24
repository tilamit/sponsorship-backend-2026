using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sponsorship.Application.Auth;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Infrastructure.Caching;
using Sponsorship.Infrastructure.Common;
using Sponsorship.Infrastructure.Identity;
using Sponsorship.Infrastructure.Persistence;
using Sponsorship.Infrastructure.Persistence.Repositories;

namespace Sponsorship.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer(
                cfg.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

        services.Configure<JwtSettings>(cfg.GetSection(JwtSettings.SectionName));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ISponsorshipRequestRepository, SponsorshipRequestRepository>();
        services.AddScoped<ISponsorshipTypeRepository, SponsorshipTypeRepository>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<ICacheService, MemoryCacheService>();

        return services;
    }
}
