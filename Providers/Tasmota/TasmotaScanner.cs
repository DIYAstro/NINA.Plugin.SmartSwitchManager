using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using NINA.Plugin.SmartSwitchManager.Core.Backends;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Backends {

    /// <summary>
    /// Scans a Tasmota device for its model and relay count.
    /// </summary>
    [Export(typeof(ISmartSwitchScanner))]
    [ExportMetadata("ProviderId", "Tasmota")]
    public class TasmotaScanner : ISmartSwitchScanner {

        public async Task<IEnumerable<DiscoveredSwitch>> ScanAsync(string ipAddress, string username, string password, CancellationToken token) {
            var discovered = new List<DiscoveredSwitch>();
            string baseUrl = $"http://{ipAddress.Trim()}/cm?";
            
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)) {
                baseUrl += $"user={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}&";
            }
            baseUrl += "cmnd=";

            var handler = new HttpClientHandler();

            using var httpClient = new HttpClient(handler) {
                Timeout = TimeSpan.FromSeconds(5)
            };

            try {
                // Tasmota command to get status
                var response = await httpClient.GetAsync($"{baseUrl}Status", token);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                string friendlyName = json["Status"]?["FriendlyName"]?[0]?.ToString() ?? "Tasmota Device";
                string model = json["Status"]?["DeviceName"]?.ToString() ?? "Generic";

                // Count relays using 'State'
                var stateResponse = await httpClient.GetAsync($"{baseUrl}State", token);
                stateResponse.EnsureSuccessStatusCode();
                var stateContent = await stateResponse.Content.ReadAsStringAsync();
                var stateJson = JObject.Parse(stateContent);

                int channel = 1;
                while (stateJson.ContainsKey($"POWER{channel}") || (channel == 1 && stateJson.ContainsKey("POWER"))) {
                    discovered.Add(new DiscoveredSwitch {
                        IpAddress = ipAddress,
                        Username = username,
                        Password = password,
                        Channel = channel,
                        DefaultName = $"{friendlyName} ({model}) - Ch {channel}",
                        ProviderType = "Tasmota"
                    });

                    // If it only has 'POWER' (no index), it's a single channel device.
                    if (!stateJson.ContainsKey($"POWER{channel}") && stateJson.ContainsKey("POWER")) {
                        break;
                    }

                    channel++;
                    if (channel > 8) break; // Most Tasmota devices don't have more than 8 relays
                }

                if (discovered.Count == 0 && stateJson.HasValues) {
                    // Fallback for single channel if it didn't find POWERx
                    discovered.Add(new DiscoveredSwitch {
                        IpAddress = ipAddress,
                        Username = username,
                        Password = password,
                        Channel = 1,
                        DefaultName = $"{friendlyName} ({model})",
                        ProviderType = "Tasmota"
                    });
                }

            } catch (Exception ex) {
                Logger.Error($"TasmotaScanner: Failed to scan {ipAddress}. {ex.Message}");
                throw;
            }

            return discovered;
        }
    }
}
