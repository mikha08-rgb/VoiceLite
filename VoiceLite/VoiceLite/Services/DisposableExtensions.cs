using System;

namespace VoiceLite.Services;

internal static class DisposableExtensions
{
    public static void SafeDispose<T>(this T? obj) where T : IDisposable
    {
        try { obj?.Dispose(); }
        catch (ObjectDisposedException) { }
    }
}
