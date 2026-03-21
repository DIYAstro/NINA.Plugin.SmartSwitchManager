using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using System;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Backends {

    /// <summary>
    /// Backend implementation for Shelly Gen 1 relay devices.
    /// Communicates via the Shelly HTTP API:
    ///   - Turn on:  GET http://{ip}/relay/{channel}?turn=on
    ///   - Turn off: GET http://{ip}/relay/{channel}?turn=off
    ///   - State:    GET http://{ip}/relay/{channel}  → JSON { "ison": true/false, ... }
    /// </summary>
    [ExportBackend("Shelly", "Shelly (Gen 1)", SupportsScanning = true, SupportsHardwareTimer = true)]
    public class ShellyGen1Backend : ShellyBackendBase {

        public ShellyGen1Backend() {
        }

        public override async Task TurnOnAsync() {
            CheckInitialized();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.GetAsync($"{baseUrl}/relay/{channel}?turn=on", cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"ShellyGen1Backend: Turned ON ({baseUrl}/relay/{channel})");
            } catch (Exception ex) {
                Logger.Error($"ShellyGen1Backend: Failed to turn ON ({baseUrl}/relay/{channel}): {ex.Message}");
                throw;
            }
        }

        public override async Task TurnOffAsync() {
            CheckInitialized();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.GetAsync($"{baseUrl}/relay/{channel}?turn=off", cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"ShellyGen1Backend: Turned OFF ({baseUrl}/relay/{channel})");
            } catch (Exception ex) {
                Logger.Error($"ShellyGen1Backend: Failed to turn OFF ({baseUrl}/relay/{channel}): {ex.Message}");
                throw;
            }
        }

        public override async Task<bool> GetStateAsync() {
            CheckInitialized();
            try {
                return await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.GetAsync($"{baseUrl}/relay/{channel}", cts.Token);
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var isOn = json.Value<bool>("ison");
                    Logger.Trace($"ShellyGen1Backend: State query ({baseUrl}/relay/{channel}) → ison={isOn}");
                    return isOn;
                });
            } catch (Exception ex) {
                Logger.Error($"ShellyGen1Backend: Failed to get state ({baseUrl}/relay/{channel}): {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sets the switch to the target state, optionally with a hardware timer.
        /// </summary>
        /// <remarks>
        /// LOGIC NOTE: Shelly Gen 1 uses the 'timer' parameter to flip the state back 
        /// after X seconds. This is handles exactly like Gen 2 'toggle_after'.
        /// </remarks>
        public override async Task SetStateAsync(bool targetState, int delaySeconds = 0) {
            CheckInitialized();
            // Safety Check: Avoid power-cycling if target state is already reached
            bool currentState = await GetStateAsync();
            if (currentState == targetState) {
                Logger.Info($"ShellyGen1Backend: Switch '{baseUrl}/relay/{channel}' is already {(targetState ? "ON" : "OFF")}. Skipping command.");
                return;
            }

            try {
                string turn;
                if (delaySeconds > 0) {
                    turn = targetState ? "off" : "on";
                } else {
                    turn = targetState ? "on" : "off";
                }

                string url = $"{baseUrl}/relay/{channel}?turn={turn}";
                if (delaySeconds > 0) {
                    url += $"&timer={delaySeconds}";
                }

                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.GetAsync(url, cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"ShellyGen1Backend: Set state to {targetState} (delay={delaySeconds}) via {url}");
            } catch (Exception ex) {
                Logger.Error($"ShellyGen1Backend: Failed to set state ({baseUrl}/relay/{channel}): {ex.Message}");
                throw;
            }
        }
    }
}
