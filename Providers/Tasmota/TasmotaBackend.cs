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
    [ExportBackend("Tasmota", "Tasmota", SupportsScanning = true, SupportsHardwareTimer = true)]
    public class TasmotaBackend : ISmartSwitchBackend {
        private string baseUrl;
        private int channel;
        private string powerCmd;
        private string username;
        private string password;
        private string ruleCommand;
        private bool isInitialized;
        private readonly System.Threading.SemaphoreSlim _httpLock = new System.Threading.SemaphoreSlim(1, 1);

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
            if (this.channel < 1) this.channel = 1;

            // Use indexed Power command for consistency (Power1, Power2...)
            this.powerCmd = $"Power{this.channel}";
            this.username = config.GetSetting("Username");
            this.password = config.GetSetting("Password");
            
            string ruleIdStr = config.GetSetting("RuleId", "3");
            if (ruleIdStr != "1" && ruleIdStr != "2") {
                ruleIdStr = "3";
            }
            this.ruleCommand = $"Rule{ruleIdStr}";
            
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
            await SetStateAsync(true, 0);
        }

        public async Task TurnOffAsync() {
            await SetStateAsync(false, 0);
        }

        public async Task<bool> GetStateAsync() {
            CheckInitialized();
            await _httpLock.WaitAsync();
            try {
                return await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    using var request = CreateRequest($"{baseUrl}/cm?cmnd={powerCmd}");
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    
                    // Tasmota returns {"POWER1":"ON"} or {"POWER":"OFF"}
                    // We prioritize the indexed key as it's the most specific.
                    string state = null;
                    string targetKey = powerCmd.ToUpper(); // e.g. POWER1

                    if (json.TryGetValue(targetKey, out var val)) {
                        state = val.ToString();
                    } else if (json.TryGetValue("POWER", out var v0)) {
                        state = v0.ToString();
                    } else {
                        // Case-insensitive fallback: scan all keys for something matching PowerX
                        foreach (var prop in json.Properties()) {
                            if (string.Equals(prop.Name, targetKey, StringComparison.OrdinalIgnoreCase) || 
                                string.Equals(prop.Name, "POWER", StringComparison.OrdinalIgnoreCase)) {
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
            } finally {
                _httpLock.Release();
            }
        }

        public bool SupportsHardwareTimer => true;

        public async Task SetStateAsync(bool targetState, int delaySeconds = 0) {
            CheckInitialized();

            // Safety Check: Avoid power-cycling if target state is already reached
            // Only if NO delay is requested. If a delay is requested, it means we want the pulse behavior.
            if (delaySeconds <= 0) {
                bool currentState = await GetStateAsync();
                if (currentState == targetState) {
                    Logger.Info($"TasmotaBackend: Switch ({baseUrl}, Ch: {channel}) is already {(targetState ? "ON" : "OFF")}. Skipping command.");
                    return;
                }

                // Use Backlog instead of separate requests to avoid Tasmota's Webserver dropping connections
                try {
                    string stateVal = targetState ? "1" : "0";
                    
                    // Bundle the commands: 
                    // 1. Reset PulseTime
                    // 2. Disable the timer rule
                    // 3. Set the actual state
                    string backlogCmd = $"Backlog {GetPulseCmd()} 0; {ruleCommand} 0; {powerCmd} {stateVal}";
                    await ExecuteCommandAsync(backlogCmd);
                    
                    Logger.Info($"TasmotaBackend: Set state to {(targetState ? "ON" : "OFF")} ({baseUrl}, Ch: {channel}) using Backlog reset");
                } catch (Exception ex) {
                    Logger.Error($"TasmotaBackend: Failed to set state: {ex.Message}");
                    throw;
                }
                return;
            }

            // Hardware Timer Logic
            try {
                int pulseTimeValue;
                if (delaySeconds <= 11) {
                    pulseTimeValue = delaySeconds * 10;
                } else {
                    pulseTimeValue = Math.Min(delaySeconds + 100, 64900);
                }

                string immediateState = !targetState ? "1" : "0";
                string targetStateVal = targetState ? "1" : "0";
                
                // One-shot timer logic: Rule triggers when target state is reached, resets PulseTime and disables itself.
                // We use explicit indices (e.g. Power1#State) for rule triggers to ensure they work reliably.
                string ruleDef = $"{ruleCommand} ON {powerCmd}#State={targetStateVal} DO {GetPulseCmd()} 0 ON {powerCmd}#State={targetStateVal} DO {ruleCommand} 0";
                // Only the timer setup uses Backlog to be as fast as possible for the pulse start.
                string timerCmd = $"Backlog {ruleDef};{ruleCommand} 1;{GetPulseCmd()} {pulseTimeValue};{powerCmd} {immediateState}";

                await ExecuteCommandAsync(timerCmd);
                Logger.Info($"TasmotaBackend: Set one-shot pulse timer for {delaySeconds}s to reach {(targetState ? "ON" : "OFF")} ({baseUrl}, Ch: {channel}) using {ruleCommand}");
            } catch (Exception ex) {
                Logger.Error($"TasmotaBackend: Failed to set state with timer: {ex.Message}");
                throw;
            }
        }

        private string GetPulseCmd() => $"PulseTime{this.channel}";

        private async Task ExecuteCommandAsync(string command) {
            await _httpLock.WaitAsync();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    using var request = CreateRequest($"{baseUrl}/cm?cmnd={Uri.EscapeDataString(command)}");
                    var response = await HttpClient.SendAsync(request, cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });

                // Tasmota answers HTTP 200 OK immediately for Backlog, but executes it slowly (~200ms per command).
                // If we send a new HTTP request (like GetStateAsync) while Backlog is running, Tasmota ABORTS the backlog!
                // We keep the lock held for 750ms to give Tasmota absolute silence to finish switching before the next poll.
                await Task.Delay(750);
            } finally {
                _httpLock.Release();
            }
        }
 
        public System.Collections.Generic.IEnumerable<ConfigFieldDescriptor> ConfigFields {
            get {
                yield return new ConfigFieldDescriptor("Host", "IP Address", ConfigFieldType.Text, true);
                yield return new ConfigFieldDescriptor("Channel", "Relay Index", ConfigFieldType.Number, true, "1");
                yield return new ConfigFieldDescriptor("Username", "User", ConfigFieldType.Text, false);
                yield return new ConfigFieldDescriptor("Password", "Pass", ConfigFieldType.Password, false);
                yield return new ConfigFieldDescriptor("RuleId", "Timer Rule ID (1-3)", ConfigFieldType.Number, false, "3") { IsExpertOnly = true };
            }
        }
        public void Dispose() {
            _httpLock?.Dispose();
        }
    }
}
