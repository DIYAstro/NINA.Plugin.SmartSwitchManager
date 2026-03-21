using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Backends {

    /// <summary>
    /// Base class for Shelly backends (Gen 1 and Gen 2+) to consolidate shared logic.
    /// Each backend instance gets its own HttpClient with its own CredentialCache,
    /// which is thread-safe and supports Digest auth (required by Shelly Gen 2/3).
    /// </summary>
    public abstract class ShellyBackendBase : ISmartSwitchBackend {
        protected string baseUrl = string.Empty;
        protected int channel;
        protected bool isInitialized;

        // Per-instance HttpClient: avoids shared credential state and supports Digest auth
        private HttpClient _httpClient;

        protected HttpClient HttpClient => _httpClient;

        public virtual void Initialize(SmartSwitchConfig config) {
            if (config == null) throw new ArgumentNullException(nameof(config));

            string host = config.GetSetting("Host");
            if (string.IsNullOrWhiteSpace(host)) {
                host = config.GetSetting("IpAddress");
            }

            if (string.IsNullOrWhiteSpace(host)) {
                throw new ArgumentException("Host/IP address must not be empty.");
            }

            baseUrl = $"http://{host.Trim()}";

            string channelStr = config.GetSetting("Channel", "0");
            int.TryParse(channelStr, out channel);

            string password = config.GetSetting("Password");
            string username = config.GetSetting("Username");

            if (string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)) {
                username = "admin";
            }

            // Build a per-instance HttpClientHandler with its own CredentialCache.
            // This is thread-safe (no shared state) and allows transparent Digest auth
            // negotiation, which Shelly Gen2/3 devices require.
            var handler = new HttpClientHandler();
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)) {
                var uri = new Uri(baseUrl);
                var credentials = new NetworkCredential(username, password);
                var credentialCache = new CredentialCache();
                credentialCache.Add(uri, "Basic", credentials);
                credentialCache.Add(uri, "Digest", credentials);  // Gen2/3 requires Digest
                handler.Credentials = credentialCache;
                handler.PreAuthenticate = true;
            }

            _httpClient = new HttpClient(handler) {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };

            isInitialized = true;
        }

        protected void CheckInitialized() {
            if (!isInitialized) throw new InvalidOperationException("Backend must be initialized before use.");
        }

        public abstract Task TurnOnAsync();
        public abstract Task TurnOffAsync();
        public abstract Task<bool> GetStateAsync();
        public abstract Task SetStateAsync(bool targetState, int delaySeconds = 0);

        public bool SupportsHardwareTimer => true;

        public virtual void Dispose() {
            _httpClient?.Dispose();
            _httpClient = null;
        }

        public virtual System.Collections.Generic.IEnumerable<ConfigFieldDescriptor> ConfigFields {
            get {
                yield return new ConfigFieldDescriptor("Host", "IP/Host", ConfigFieldType.Text, true);
                yield return new ConfigFieldDescriptor("Channel", "Relay Index", ConfigFieldType.Number, true, "0");
                yield return new ConfigFieldDescriptor("Username", "User", ConfigFieldType.Text, false, "admin");
                yield return new ConfigFieldDescriptor("Password", "Pass", ConfigFieldType.Password, false);
            }
        }
    }
}
