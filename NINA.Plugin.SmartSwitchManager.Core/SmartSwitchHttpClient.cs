using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Core {

    /// <summary>
    /// Shared HTTP infrastructure for smart switch providers.
    /// </summary>
    public static class SmartSwitchHttpClient {
        public static HttpClient Instance { get; private set; }

        /// <summary>
        /// Global default timeout in seconds for all requests.
        /// Can be changed at runtime as it's used to create per-request tokens.
        /// </summary>
        public static int TimeoutSeconds { get; set; } = 10;

        static SmartSwitchHttpClient() {
            // We use an infinite timeout on the client instance itself because 
            // mutating it after the first request would throw an InvalidOperationException.
            // Instead, we use per-request CancellationTokens for the actual timeout.
            Instance = new HttpClient() {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };
        }

        /// <summary>
        /// Creates a CancellationTokenSource that expires after the current TimeoutSeconds.
        /// </summary>
        public static System.Threading.CancellationTokenSource GetCts() {
            return new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds)));
        }

        /// <summary>
        /// Executes an asynchronous network action with a simple linear backoff retry policy.
        /// Handles HttpRequestException and TaskCanceledException (Timeout).
        /// </summary>
        public static async Task<T> ExecuteWithRetry<T>(Func<Task<T>> action, int maxRetries = 2, int delayMs = 1000) {
            int retryCount = 0;
            while (true) {
                try {
                    return await action();
                } catch (Exception ex) when (retryCount < maxRetries && (ex is HttpRequestException || ex is System.Threading.Tasks.TaskCanceledException)) {
                    retryCount++;
                    NINA.Core.Utility.Logger.Debug($"SmartSwitchHttpClient: Action failed, retrying ({retryCount}/{maxRetries})... Error: {ex.Message}");
                    await Task.Delay(delayMs * retryCount);
                }
            }
        }
    }
}
