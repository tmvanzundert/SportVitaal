using Microsoft.Extensions.DependencyInjection;
using SportVitaal.Domain.DomainEvents;
using SportVitaal.Domain.Services;
using SportVitaal.Application.Services;

namespace SportVitaal.Application.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IReservationService, ReservationService>();
            services.AddScoped<IMembershipService, MembershipService>();

            // Domain event dispatcher (simple console implementation in Application for dev/demo)
            services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

            return services;
        }
    }
}

