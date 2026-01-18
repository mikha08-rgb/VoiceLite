using System;

namespace VoiceLite.Services;

internal static class DisposableExtensions
{
    /// <summary>
    /// Safely disposes an object, catching all exceptions during disposal.
    /// This prevents disposal errors from masking original exceptions or crashing the application.
    /// </summary>
    public static void SafeDispose<T>(this T? obj) where T : class, IDisposable
    {
        try { obj?.Dispose(); }
        catch (Exception)
        {
            // Swallow all exceptions during disposal to prevent:
            // 1. ObjectDisposedException - already disposed
            // 2. InvalidOperationException - disposed in invalid state
            // 3. Any other disposal errors that shouldn't crash the app
        }
    }
}
