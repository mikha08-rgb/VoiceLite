using Microsoft.Extensions.DependencyInjection;
using VoiceLite.Core.Controllers;
using VoiceLite.Core.Interfaces.Controllers;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Models;
using VoiceLite.Presentation.ViewModels;
using VoiceLite.Services;

namespace VoiceLite.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Configures dependency injection services for the application
    /// </summary>
    public static class ServiceConfiguration
    {
        /// <summary>
        /// Registers all application services
        /// </summary>
        public static IServiceCollection AddVoiceLiteServices(this IServiceCollection services)
        {
            // Register Settings as singleton - shared across all services
            services.AddSingleton(provider => Settings.Load());

            // Register Core Services (Singleton - single instance for app lifetime)
            services.AddSingleton<IAudioRecorder, AudioRecorder>();
            services.AddSingleton<IWhisperService, PersistentWhisperService>();
            services.AddSingleton<ITextInjector, TextInjector>();
            services.AddSingleton<IErrorLogger, ErrorLoggerService>();
            services.AddSingleton<IHotkeyManager, HotkeyManager>();
            services.AddSingleton<ISystemTrayManager, SystemTrayManager>();

            // Register Feature Services (Singleton - state is maintained)
            services.AddSingleton<ILicenseService, LicenseService>();
            services.AddSingleton<IProFeatureService, ProFeatureService>();
            services.AddSingleton<ITranscriptionHistoryService, TranscriptionHistoryService>();
            services.AddSingleton<ICustomShortcutService, CustomShortcutService>();
            services.AddSingleton<ISettingsService, SettingsService>();

            // Register Controllers (Scoped - new instance per request/window)
            services.AddScoped<IRecordingController, RecordingController>();
            services.AddScoped<ITranscriptionController, TranscriptionController>();

            // Register ViewModels (Transient - new instance every time)
            services.AddTransient<MainViewModel>();
            services.AddTransient<SettingsViewModel>();

            // Register Windows (Transient - new instance for each window)
            services.AddTransient<MainWindow>();
            services.AddTransient<SettingsWindowNew>();

            return services;
        }

        /// <summary>
        /// Registers infrastructure services
        /// </summary>
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            // Add any infrastructure services here (logging, configuration, etc.)
            // For now, we'll use the existing ErrorLogger static class

            return services;
        }

        /// <summary>
        /// Configures service options
        /// </summary>
        public static IServiceCollection ConfigureOptions(this IServiceCollection services)
        {
            // Configure any service options here
            // For example, HttpClient configurations, timeouts, etc.

            return services;
        }
    }
}