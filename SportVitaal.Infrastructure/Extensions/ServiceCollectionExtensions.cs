using Microsoft.Extensions.DependencyInjection;
using SportVitaal.Domain.Repositories;
using SportVitaal.Infrastructure.Repositories;
using SportVitaal.Domain.Services;
using SportVitaal.Infrastructure.Services.Notifications;
using SportVitaal.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SportVitaal.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers Infrastructure implementations for Domain repository interfaces and UnitOfWork.
        /// </summary>
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            // Repositories
            services.AddScoped<ILessonRepository, LessonRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IWorkoutRepository, WorkoutRepository>();
            services.AddScoped<ILocationRepository, LocationRepository>();
            services.AddScoped<IReservationRepository, ReservationRepository>();
            services.AddScoped<IWaitingListRepository, WaitingListRepository>();
            services.AddScoped<IMembershipRepository, MembershipRepository>();

            // Unit of Work
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Notifications - prefer SMTP sender (configured via appsettings), fallback console is still available.
            services.AddOptions<SmtpOptions>().Bind(services.BuildServiceProvider().GetRequiredService<IConfiguration>().GetSection("Smtp"));
            services.AddScoped<INotificationService, SmtpEmailSender>();

            // Payment integration: use simulated in-memory payment service for now (no external provider)
            services.AddScoped<IPaymentService, Payments.SimulatedPaymentService>();

            // Hosted services
            services.AddHostedService<HostedServices.MembershipExpiryHostedService>();

            return services;
        }
    }
}

