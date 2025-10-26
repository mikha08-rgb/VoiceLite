using System;
using Microsoft.Extensions.DependencyInjection;

namespace VoiceLite.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Provides global access to the dependency injection service provider
    /// </summary>
    public static class ServiceProviderWrapper
    {
        private static IServiceProvider? _serviceProvider;

        /// <summary>
        /// Gets the current service provider
        /// </summary>
        public static IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider == null)
                {
                    throw new InvalidOperationException(
                        "ServiceProvider has not been initialized. " +
                        "Please ensure the application has started correctly.");
                }
                return _serviceProvider;
            }
        }

        /// <summary>
        /// Initializes the service provider
        /// </summary>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Gets a required service from the container
        /// </summary>
        public static T GetRequiredService<T>() where T : notnull
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Gets a service from the container, or null if not registered
        /// </summary>
        public static T? GetService<T>() where T : class
        {
            return ServiceProvider.GetService<T>();
        }

        /// <summary>
        /// Creates a new scope for scoped services
        /// </summary>
        public static IServiceScope CreateScope()
        {
            return ServiceProvider.CreateScope();
        }

        /// <summary>
        /// Resets the service provider (for testing purposes)
        /// </summary>
        internal static void Reset()
        {
            _serviceProvider = null;
        }
    }
}