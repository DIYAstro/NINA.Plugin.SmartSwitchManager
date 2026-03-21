using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Backends {

    /// <summary>
    /// Backend for ESPHome devices using the web_server REST API.
    /// URL: http://<ip>/switch/<id>/turn_on
    /// status: GET /switch/<id> returns JSON
    /// </summary>
    [ExportBackend("ESPHome", "ESPHome", SupportsHardwareTimer = false)]
    public class ESPHomeBackend : ISmartSwitchBackend {
        private string baseUrl = string.Empty;
        private string entityId = string.Empty;
        private string username = string.Empty;
        private string password = string.Empty;
        private bool isInitialized;

        public ESPHomeBackend() {
        }

        public void Initialize(SmartSwitchConfig config) {
            if (config == null) throw new ArgumentNullException(nameof(config));
            
            string host = config.GetSetting("Host");
            if (string.IsNullOrWhiteSpace(host)) {
                throw new ArgumentException("IP Address / Host must not be empty.");
            }
            
            this.entityId = config.GetSetting("EntityId");
            if (string.IsNullOrWhiteSpace(this.entityId)) {
                throw new ArgumentException("Entity ID must not be empty.");
            }

            // Ensure URL starts with http://
            if (!host.Contains("://")) {
                host = "http://" + host;
            }
            baseUrl = host.TrimEnd('/');
            this.username = config.GetSetting("Username");
            this.password = config.GetSetting("Password");
            isInitialized = true;
        }

        private void CheckInitialized() {
            if (!isInitialized) throw new InvalidOperationException("Backend must be initialized before use.");
        }

        private HttpClient HttpClient => SmartSwitchHttpClient.Instance;

        private HttpRequestMessage CreateRequest(HttpMethod method, string? action) {
            // ESPHome entity IDs from discovery look like "switch-my_relay"
            // The REST API expects /switch/switch-my_relay/turn_on
            
            string domain = "switch";
            if (entityId.StartsWith("light-", StringComparison.OrdinalIgnoreCase)) {
                domain = "light";
            } else if (entityId.StartsWith("fan-", StringComparison.OrdinalIgnoreCase)) {
                domain = "fan";
            }

            string url = $"{baseUrl}/{domain}/{entityId}";
            if (!string.IsNullOrEmpty(action)) {
                url += $"/{action}";
            }

            var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)) {
                var authHeader = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
            }
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }

        public async Task TurnOnAsync() {
            CheckInitialized();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var request = CreateRequest(HttpMethod.Post, "turn_on");
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"ESPHomeBackend: Turned ON {entityId}");
            } catch (Exception ex) {
                Logger.Error($"ESPHomeBackend: Failed to turn ON {entityId}: {ex.Message}");
                throw;
            }
        }

        public async Task TurnOffAsync() {
            CheckInitialized();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var request = CreateRequest(HttpMethod.Post, "turn_off");
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"ESPHomeBackend: Turned OFF {entityId}");
            } catch (Exception ex) {
                Logger.Error($"ESPHomeBackend: Failed to turn OFF {entityId}: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> GetStateAsync() {
            CheckInitialized();
            try {
                return await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    using var request = CreateRequest(HttpMethod.Get, null);
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    
                    if (json.TryGetValue("value", out var value) && value != null) {
                        return value.Value<bool>();
                    }
                    
                    if (json.TryGetValue("state", out var state) && state != null) {
                        return string.Equals(state.ToString(), "ON", StringComparison.OrdinalIgnoreCase);
                    }

                    throw new Exception($"Could not find state for {entityId} in response: {content}");
                });
            } catch (Exception ex) {
                Logger.Error($"ESPHomeBackend: Failed to get state for {entityId}: {ex.Message}");
                throw;
            }
        }

        public bool SupportsHardwareTimer => false;

        public async Task SetStateAsync(bool targetState, int delaySeconds = 0) {
            CheckInitialized();
            if (targetState) {
                await TurnOnAsync();
            } else {
                await TurnOffAsync();
            }
        }
 
        public System.Collections.Generic.IEnumerable<ConfigFieldDescriptor> ConfigFields {
            get {
                yield return new ConfigFieldDescriptor("Host", "IP Address", ConfigFieldType.Text, true);
                yield return new ConfigFieldDescriptor("EntityId", "Entity ID", ConfigFieldType.Text, true);
                yield return new ConfigFieldDescriptor("Username", "User", ConfigFieldType.Text, false);
                yield return new ConfigFieldDescriptor("Password", "Pass", ConfigFieldType.Password, false);
            }
        }
        public void Dispose() {
            // No specific per-instance resources to dispose.
        }
    }
}
