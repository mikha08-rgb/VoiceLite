using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using VoiceLite.Services;

namespace VoiceLite.Infrastructure.Resilience
{
    /// <summary>
    /// PHASE 3 - DAY 1: Centralized retry policies for resilient operations
    ///
    /// Purpose: Provides reusable retry policies with exponential backoff for transient failures
    /// Used by: LicenseService (network), PersistentWhisperService (process), ModelDownloadService (I/O)
    ///
    /// Architecture: Policy objects are thread-safe and reusable (singleton pattern)
    /// </summary>
    public static class RetryPolicies
    {
        /// <summary>
        /// HTTP Retry Policy - Handles transient network failures
        ///
        /// Retries: 3 attempts
        /// Backoff: Exponential (1s, 2s, 4s)
        /// Triggers: HttpRequestException, TaskCanceledException, 5xx status codes
        ///
        /// Use Case: License validation API calls, model downloads
        /// </summary>
        public static readonly AsyncRetryPolicy<HttpResponseMessage> HttpRetryPolicy =
            Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>() // Timeout
                .OrResult(r => !r.IsSuccessStatusCode && IsTransientHttpError(r.StatusCode))
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var statusCode = outcome.Result?.StatusCode.ToString() ?? "N/A";
                        var exception = outcome.Exception?.Message ?? "No exception";

                        ErrorLogger.LogWarning(
                            $"HTTP Retry {retryCount}/3 after {timespan.TotalSeconds:F1}s | " +
                            $"Status: {statusCode} | Exception: {exception}"
                        );
                    });

        /// <summary>
        /// Determines if an HTTP status code represents a transient error worth retrying
        /// </summary>
        /// <param name="statusCode">HTTP status code to evaluate</param>
        /// <returns>True if transient (5xx errors), false otherwise</returns>
        private static bool IsTransientHttpError(HttpStatusCode statusCode)
        {
            // Retry on server errors (5xx) - likely temporary issues
            // Don't retry on client errors (4xx) - likely permanent issues (bad request, unauthorized, etc.)
            return (int)statusCode >= 500 && (int)statusCode < 600;
        }

        /// <summary>
        /// Process Retry Policy - Handles Whisper.exe process crashes
        ///
        /// Retries: 2 attempts (process failures are more expensive)
        /// Backoff: Fixed 500ms delay (process startup overhead)
        /// Triggers: ExternalException, ProcessException
        ///
        /// Use Case: PersistentWhisperService.TranscribeAsync()
        /// Note: This is separate from circuit breaker (Day 2) which handles repeated failures
        /// </summary>
        public static readonly AsyncRetryPolicy ProcessRetryPolicy =
            Policy
                .Handle<System.ComponentModel.Win32Exception>() // Process start failures
                .Or<System.Runtime.InteropServices.ExternalException>() // Whisper.exe exit code != 0
                .Or<InvalidOperationException>(ex => ex.Message.Contains("process")) // Process-related errors
                .WaitAndRetryAsync(
                    retryCount: 2,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(500),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        ErrorLogger.LogWarning(
                            $"Process Retry {retryCount}/2 after {timespan.TotalMilliseconds}ms | " +
                            $"Exception: {exception.Message}"
                        );
                    });

        /// <summary>
        /// File I/O Retry Policy - Handles transient file system errors
        ///
        /// Retries: 3 attempts
        /// Backoff: Linear (200ms, 400ms, 600ms)
        /// Triggers: IOException, UnauthorizedAccessException
        ///
        /// Use Case: Model file downloads, settings file writes
        /// </summary>
        public static readonly AsyncRetryPolicy FileIORetryPolicy =
            Policy
                .Handle<System.IO.IOException>() // File locked, permission denied, disk full
                .Or<UnauthorizedAccessException>() // Permission issues
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(200 * retryAttempt),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        ErrorLogger.LogWarning(
                            $"File I/O Retry {retryCount}/3 after {timespan.TotalMilliseconds}ms | " +
                            $"Exception: {exception.Message}"
                        );
                    });
    }
}
