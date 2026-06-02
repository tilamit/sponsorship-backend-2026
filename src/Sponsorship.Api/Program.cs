using System.IO.Compression;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.ResponseCompression;
using Serilog;
using Sponsorship.Api.Extensions;
using Sponsorship.Api.Middleware;
using Sponsorship.Api.Services;
using Sponsorship.Application;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Infrastructure;
using Sponsorship.Infrastructure.Persistence;
using Sponsorship.Infrastructure.Persistence.Seed;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/app-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7));

    builder.Services
        .AddInfrastructure(builder.Configuration)
        .AddApplication()
        .AddJwtAuth(builder.Configuration)
        .AddSwaggerWithJwt()
        .AddSponsorshipRateLimiting();

    builder.Services.AddMemoryCache(o =>
    {
        o.SizeLimit = 1024;
        o.CompactionPercentage = 0.20;
    });

    builder.Services.AddResponseCompression(o =>
    {
        o.EnableForHttps = true;
        o.Providers.Add<BrotliCompressionProvider>();
        o.Providers.Add<GzipCompressionProvider>();
        o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/json" });
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

    builder.Services.Configure<CookiePolicyOptions>(o =>
    {
        o.MinimumSameSitePolicy = SameSiteMode.Strict;
        o.HttpOnly = HttpOnlyPolicy.Always;
        o.Secure = CookieSecurePolicy.Always;
    });

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();

    builder.Services.AddControllers();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? Array.Empty<string>();
    builder.Services.AddCors(opts =>
    {
        opts.AddPolicy("AllowFrontend", p => p
            .WithOrigins(allowedOrigins)
            .WithHeaders("Content-Type", "Authorization")
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .AllowCredentials());
    });

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await DbSeeder.SeedAsync(db, hasher);
    }

    app.UseResponseCompression();
    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sponsorship API v1"));

    app.UseHttpsRedirection();

    // CORS must run before authentication so preflight succeeds. Cookie policy + rate limiter
    // come after auth so the partition key can use the authenticated user.
    app.UseCors("AllowFrontend");
    app.UseCookiePolicy();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();

    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
