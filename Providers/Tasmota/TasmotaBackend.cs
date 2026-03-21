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
    /// Backend for Tasmota devices using the HTTP API.
    /// Commands: cm?cmnd=Power<CH>%20On, cm?cmnd=Power<CH>%20Off, cm?cmnd=Power<CH>
    /// </summary>
    [ExportBackend("Tasmota", "Tasmota", SupportsScanning = true)]
    public class TasmotaBackend : ISmartSwitchBackend {
        private string baseUrl;
        private int channel;
        private string powerCmd;
        private string username;
        private string password;
        private bool isInitialized;

        public TasmotaBackend() {
        }

        public void Initialize(SmartSwitchConfig config) {
            if (config == null) throw new ArgumentNullException(nameof(config));
            
            string host = config.GetSetting("Host");
            if (string.IsNullOrWhiteSpace(host)) {
                throw new ArgumentException("Host URL / IP must not be empty.");
            }

            // Ensure URL starts with http://
            if (!host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                host = "http://" + host;
            }
            baseUrl = host.TrimEnd('/');
            
            string channelStr = config.GetSetting("Channel", "1");
            int.TryParse(channelStr, out this.channel);

            this.powerCmd = channel <= 1 ? "Power" : $"Power{channel}";
            this.username = config.GetSetting("Username");
            this.password = config.GetSetting("Password");
            isInitialized = true;
        }

        private void CheckInitialized() {
            if (!isInitialized) throw new InvalidOperationException("Backend must be initialized before use.");
        }

        private HttpClient HttpClient => SmartSwitchHttpClient.Instance;

        private HttpRequestMessage CreateRequest(string url) {
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)) {
                string separator = url.Contains("?") ? "&" : "?";
                url += $"{separator}user={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}";
            }
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            return request;
        }

        public async Task TurnOnAsync() {
            CheckInitialized();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    using var request = CreateRequest($"{baseUrl}/cm?cmnd={powerCmd}%20On");
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"TasmotaBackend: Turned ON ({baseUrl}, Ch: {channel})");
            } catch (Exception ex) {
                Logger.Error($"TasmotaBackend: Failed to turn ON: {ex.Message}");
                throw;
            }
        }

        public async Task TurnOffAsync() {
            CheckInitialized();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    using var request = CreateRequest($"{baseUrl}/cm?cmnd={powerCmd}%20Off");
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"TasmotaBackend: Turned OFF ({baseUrl}, Ch: {channel})");
            } catch (Exception ex) {
                Logger.Error($"TasmotaBackend: Failed to turn OFF: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> GetStateAsync() {
            CheckInitialized();
            try {
                return await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    using var request = CreateRequest($"{baseUrl}/cm?cmnd={powerCmd}");
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    
                    // Tasmota returns {"POWER":"ON"} or {"POWER1":"OFF"}
                    // We need to be robust here as different firmware versions/configs 
                    // might use POWER, Power, power, or POWER1 even for single-relay devices.
                    string state = null;
                    string targetKey = powerCmd.ToUpper();

                    if (json.TryGetValue(targetKey, out var val)) {
                        state = val.ToString();
                    } else if (channel <= 1 && json.TryGetValue("POWER", out var v0)) {
                        state = v0.ToString();
                    } else if (channel <= 1 && json.TryGetValue("POWER1", out var v1)) {
                        state = v1.ToString();
                    } else {
                        // Case-insensitive fallback: scan all keys
                        foreach (var prop in json.Properties()) {
                            if (string.Equals(prop.Name, targetKey, StringComparison.OrdinalIgnoreCase)) {
                                state = prop.Value.ToString();
                                break;
                            }
                        }
                    }

                    if (state == null) {
                        throw new Exception($"Could not find state for {powerCmd} in response: {content}");
                    }

                    return string.Equals(state, "ON", StringComparison.OrdinalIgnoreCase);
                });
            } catch (Exception ex) {
                Logger.Error($"TasmotaBackend: Failed to get state: {ex.Message}");
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
                yield return new ConfigFieldDescriptor("Channel", "Relay Index", ConfigFieldType.Number, true, "1");
                yield return new ConfigFieldDescriptor("Username", "User", ConfigFieldType.Text, false);
                yield return new ConfigFieldDescriptor("Password", "Pass", ConfigFieldType.Password, false);
            }
        }
        public void Dispose() {
            // No specific per-instance resources to dispose yet, as it uses the shared static client.
        }
    }
}
