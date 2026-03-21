using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Backends {

    /// <summary>
    /// Backend implementation for Home Assistant.
    /// Communicates via the Home Assistant REST API:
    ///   - Turn on:  POST {baseUrl}/api/services/{domain}/turn_on
    ///   - Turn off: POST {baseUrl}/api/services/{domain}/turn_off
    ///   - State:    GET {baseUrl}/api/states/{entity_id}
    /// </summary>
    [ExportBackend("HomeAssistant", "Home Assistant", SupportsHardwareTimer = false)]
    public class HomeAssistantBackend : ISmartSwitchBackend {
        private string baseUrl = string.Empty;
        private string entityId = string.Empty;
        private string haToken = string.Empty;
        private string turnOnService = "turn_on";
        private string turnOffService = "turn_off";
        private bool isInitialized;

        public HomeAssistantBackend() {
        }

        public void Initialize(SmartSwitchConfig config) {
            if (config == null) throw new ArgumentNullException(nameof(config));
            
            // Read and validate basic settings
            string host = config.GetSetting("Host");
            if (string.IsNullOrWhiteSpace(host)) {
                throw new ArgumentException("Home Assistant IP or URL must not be empty.");
            }
            
            this.entityId = config.GetSetting("EntityId");
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Entity ID must not be empty.");
            }

            // Read expert settings (with fallbacks)
            bool useSsl;
            if (!bool.TryParse(config.GetSetting("UseSSL", "False"), out useSsl)) {
                useSsl = false;
            }
            
            int port;
            if (!int.TryParse(config.GetSetting("Port", "8123"), out port)) {
                port = 8123;
            }

            string pathPrefix = config.GetSetting("PathPrefix", "").Trim();
            if (!string.IsNullOrEmpty(pathPrefix) && !pathPrefix.StartsWith("/")) {
                pathPrefix = "/" + pathPrefix; // Ensure leading slash
            }

            this.turnOnService = config.GetSetting("TurnOnService", "turn_on").Trim();
            if (string.IsNullOrEmpty(this.turnOnService)) this.turnOnService = "turn_on";

            this.turnOffService = config.GetSetting("TurnOffService", "turn_off").Trim();
            if (string.IsNullOrEmpty(this.turnOffService)) this.turnOffService = "turn_off";

            // --- Smart URL Building ---
            host = host.Trim();
            string protocol = useSsl ? "https://" : "http://";

            // If user explicitly typed http:// or https://, respect it and strip it for parsing
            if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) {
                protocol = "http://";
                host = host.Substring(7);
            } else if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                protocol = "https://";
                host = host.Substring(8);
            }

            host = host.TrimEnd('/'); // Clean trailing slashes

            // If user didn't provide a port in the host string, append the expert setting port
            string finalHostAndPort = host;
            if (!host.Contains(":")) {
                finalHostAndPort = $"{host}:{port}";
            }

            // Combine building blocks
            baseUrl = $"{protocol}{finalHostAndPort}{pathPrefix}".TrimEnd('/');
            
            haToken = config.GetSetting("Token")?.Trim() ?? string.Empty;
            isInitialized = true;
        }

        private void CheckInitialized() {
            if (!isInitialized) throw new InvalidOperationException("Backend must be initialized before use.");
        }

        private HttpClient HttpClient => SmartSwitchHttpClient.Instance;

        private HttpRequestMessage CreateRequest(HttpMethod method, string url) {
            var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrWhiteSpace(haToken)) {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", haToken);
            }
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }

        public async Task TurnOnAsync() {
            CheckInitialized();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    var jsonBody = JsonConvert.SerializeObject(new { entity_id = entityId });
                    var parts = entityId.Split('.');
                    var domain = parts.Length > 1 ? parts[0] : "switch";
                    
                    using var request = CreateRequest(HttpMethod.Post, $"{baseUrl}/api/services/{domain}/{turnOnService}");
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"HomeAssistantBackend: Turned ON ({entityId})");
            } catch (Exception ex) {
                Logger.Error($"HomeAssistantBackend: Failed to turn ON ({entityId}): {ex.Message}");
                throw;
            }
        }

        public async Task TurnOffAsync() {
            CheckInitialized();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    var jsonBody = JsonConvert.SerializeObject(new { entity_id = entityId });
                    var parts = entityId.Split('.');
                    var domain = parts.Length > 1 ? parts[0] : "switch";
                    
                    using var request = CreateRequest(HttpMethod.Post, $"{baseUrl}/api/services/{domain}/{turnOffService}");
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"HomeAssistantBackend: Turned OFF ({entityId})");
            } catch (Exception ex) {
                Logger.Error($"HomeAssistantBackend: Failed to turn OFF ({entityId}): {ex.Message}");
                throw;
            }
        }

        public async Task<bool> GetStateAsync() {
            CheckInitialized();
            try {
                return await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    using var request = CreateRequest(HttpMethod.Get, $"{baseUrl}/api/states/{entityId}");
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(responseContent);
                    var stateString = json.Value<string>("state");
                    
                    var isOn = string.Equals(stateString, "on", StringComparison.OrdinalIgnoreCase);
                    Logger.Trace($"HomeAssistantBackend: State query ({entityId}) → state='{stateString}', ison={isOn}");
                    return isOn;
                });
            } catch (Exception ex) {
                Logger.Error($"HomeAssistantBackend: Failed to get state ({entityId}): {ex.Message}");
                throw;
            }
        }

        public bool SupportsHardwareTimer => false;

        public async Task SetStateAsync(bool targetState, int delaySeconds = 0) {
            CheckInitialized();
            // Home Assistant REST API doesn't support a simple one-shot hardware timer parameter.
            if (targetState) {
                await TurnOnAsync();
            } else {
                await TurnOffAsync();
            }
        }
 
        public System.Collections.Generic.IEnumerable<ConfigFieldDescriptor> ConfigFields {
            get {
                yield return new ConfigFieldDescriptor("Host", "IP / Host", ConfigFieldType.Text, true);
                yield return new ConfigFieldDescriptor("Token", "Long-Lived Token", ConfigFieldType.Password, true);
                yield return new ConfigFieldDescriptor("EntityId", "Entity ID", ConfigFieldType.Text, true);

                // Expert Fields (Hidden by default unless "Expert Mode" is checked)
                yield return new ConfigFieldDescriptor("UseSSL", "Use SSL (HTTPS)", ConfigFieldType.Boolean) { IsExpertOnly = true, DefaultValue = "False" };
                yield return new ConfigFieldDescriptor("Port", "Port", ConfigFieldType.Number) { IsExpertOnly = true, DefaultValue = "8123" };
                yield return new ConfigFieldDescriptor("PathPrefix", "Path Prefix", ConfigFieldType.Text) { IsExpertOnly = true, DefaultValue = "" };
                yield return new ConfigFieldDescriptor("TurnOnService", "Turn On Service", ConfigFieldType.Text) { IsExpertOnly = true, DefaultValue = "turn_on" };
                yield return new ConfigFieldDescriptor("TurnOffService", "Turn Off Service", ConfigFieldType.Text) { IsExpertOnly = true, DefaultValue = "turn_off" };
            }
        }
        public void Dispose() {
            // No specific per-instance resources to dispose yet.
        }
    }
}
