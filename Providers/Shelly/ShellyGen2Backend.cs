using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using System;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Backends {

    /// <summary>
    /// Backend implementation for Shelly Gen 2 / Gen 3 relay devices (Plus, Pro series).
    /// Communicates via the Shelly RPC HTTP API:
    ///   - Turn on:  GET http://{ip}/rpc/Switch.Set?id={channel}&on=true
    ///   - Turn off: GET http://{ip}/rpc/Switch.Set?id={channel}&on=false
    ///   - State:    GET http://{ip}/rpc/Switch.GetStatus?id={channel}  → JSON { "output": true/false, ... }
    /// </summary>
    [ExportBackend("ShellyGen2", "Shelly (Gen 2/3)", SupportsScanning = true, SupportsHardwareTimer = true)]
    public class ShellyGen2Backend : ShellyBackendBase {

        public ShellyGen2Backend() {
        }

        public override async Task TurnOnAsync() {
            CheckInitialized();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.GetAsync($"{baseUrl}/rpc/Switch.Set?id={channel}&on=true", cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"ShellyGen2Backend: Turned ON ({baseUrl}/rpc/Switch.Set?id={channel})");
            } catch (Exception ex) {
                Logger.Error($"ShellyGen2Backend: Failed to turn ON ({baseUrl}/rpc/Switch.Set?id={channel}): {ex.Message}");
                throw;
            }
        }

        public override async Task TurnOffAsync() {
            CheckInitialized();
            try {
                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.GetAsync($"{baseUrl}/rpc/Switch.Set?id={channel}&on=false", cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"ShellyGen2Backend: Turned OFF ({baseUrl}/rpc/Switch.Set?id={channel})");
            } catch (Exception ex) {
                Logger.Error($"ShellyGen2Backend: Failed to turn OFF ({baseUrl}/rpc/Switch.Set?id={channel}): {ex.Message}");
                throw;
            }
        }

        public override async Task<bool> GetStateAsync() {
            CheckInitialized();
            try {
                return await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.GetAsync($"{baseUrl}/rpc/Switch.GetStatus?id={channel}", cts.Token);
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var isOn = json.Value<bool>("output");
                    Logger.Trace($"ShellyGen2Backend: State query ({baseUrl}/rpc/Switch.GetStatus?id={channel}) → output={isOn}");
                    return isOn;
                });
            } catch (Exception ex) {
                Logger.Error($"ShellyGen2Backend: Failed to get state ({baseUrl}/rpc/Switch.GetStatus?id={channel}): {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sets the switch to the target state, optionally with a hardware timer.
        /// </summary>
        /// <remarks>
        /// LOGIC NOTE: Shelly's 'toggle_after' (hardware timer) always flips the state back 
        /// after the specified duration. 
        /// 
        /// To reach 'targetState = OFF' after 30s:
        ///   1. We must ensure the device is currently ON.
        ///   2. We send 'on=true' with 'toggle_after=30'.
        ///   3. Shelly stays ON for 30s, then toggles to OFF.
        /// 
        /// If the device is already in the target state, we skip the command to avoid 
        /// unsolicited power cycles (Safety Check).
        /// </remarks>
        public override async Task SetStateAsync(bool targetState, int delaySeconds = 0) {
            CheckInitialized();
            // Safety Check: Avoid power-cycling if target state is already reached
            bool currentState = await GetStateAsync();
            if (currentState == targetState) {
                Logger.Info($"ShellyGen2Backend: Switch '{baseUrl}/rpc/Switch.Set?id={channel}' is already {(targetState ? "ON" : "OFF")}. Skipping command.");
                return;
            }

            try {
                bool immediateOn;
                if (delaySeconds > 0) {
                    immediateOn = !targetState;
                } else {
                    immediateOn = targetState;
                }

                string url = $"{baseUrl}/rpc/Switch.Set?id={channel}&on={immediateOn.ToString().ToLower()}";
                if (delaySeconds > 0) {
                    url += $"&toggle_after={delaySeconds}";
                }

                await SmartSwitchHttpClient.ExecuteWithRetry(async () => {
                    using var cts = SmartSwitchHttpClient.GetCts();
                    var response = await HttpClient.GetAsync(url, cts.Token);
                    response.EnsureSuccessStatusCode();
                    return true;
                });
                Logger.Info($"ShellyGen2Backend: Set state to {targetState} (delay={delaySeconds}) via {url}");
            } catch (Exception ex) {
                Logger.Error($"ShellyGen2Backend: Failed to set state ({baseUrl}/rpc/Switch.Set?id={channel}): {ex.Message}");
                throw;
            }
        }
    }
}
