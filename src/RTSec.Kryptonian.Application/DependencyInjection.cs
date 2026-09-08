using Microsoft.Extensions.DependencyInjection;
using RTSec.Kryptonian.Application.Notifications;
using RTSec.Kryptonian.Application.Services;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application;

/// <summary>
/// Extension methods for configuring application services.
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Adds application layer services to the service collection.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register application services
        services.AddScoped<ICaBackendService, CaBackendService>();
        services.AddScoped<IEstProfileService, EstProfileService>();
        services.AddScoped<IEnrollmentEventService, EnrollmentEventService>();
        services.AddScoped<IEnrollmentOrchestrator, EnrollmentOrchestrator>();
        services.AddScoped<ICertificateRevocationService, CertificateRevocationService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IGatewaySettingsService, GatewaySettingsService>();
        services.AddScoped<INotificationSettingsService, NotificationSettingsService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
