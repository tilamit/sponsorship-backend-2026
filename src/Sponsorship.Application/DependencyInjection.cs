using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Sponsorship.Application.Auth;
using Sponsorship.Application.Requests;

namespace Sponsorship.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISponsorshipRequestService, SponsorshipRequestService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<ISponsorshipTypeService, SponsorshipTypeService>();
        return services;
    }
}
