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
    /// Scans a given IP address for a Shelly device, detects its generation (Gen 1 vs Gen 2/3),
    /// and discovers all available relay channels.
    /// </summary>
    [Export(typeof(ISmartSwitchScanner))]
    [ExportMetadata("ProviderId", "Shelly")]
    public class ShellyScanner : ISmartSwitchScanner {

        public async Task<IEnumerable<DiscoveredSwitch>> ScanAsync(string ipAddress, string username, string password, CancellationToken token) {
            var discovered = new List<DiscoveredSwitch>();
            string baseUrl = $"http://{ipAddress.Trim()}";

            string effUsername = username;
            if (string.IsNullOrWhiteSpace(effUsername) && !string.IsNullOrWhiteSpace(password)) {
                effUsername = "admin";
            }

            var handler = new HttpClientHandler();
            if (!string.IsNullOrWhiteSpace(effUsername) && !string.IsNullOrWhiteSpace(password)) {
                var uri = new Uri(baseUrl);
                var credentials = new NetworkCredential(effUsername, password);
                var credentialCache = new CredentialCache();
                credentialCache.Add(uri, "Basic", credentials);
                credentialCache.Add(uri, "Digest", credentials);
                
                handler.Credentials = credentialCache;
                handler.PreAuthenticate = true;
            }

            using var httpClient = new HttpClient(handler) {
                Timeout = TimeSpan.FromSeconds(5)
            };

            try {
                // 1. Determine Generation
                var infoResponse = await httpClient.GetAsync($"{baseUrl}/shelly", token);
                infoResponse.EnsureSuccessStatusCode();
                var infoContent = await infoResponse.Content.ReadAsStringAsync();
                var infoJson = JObject.Parse(infoContent);

                BackendVersion_Local generation = BackendVersion_Local.V1; 
                string app = infoJson.Value<string>("app") ?? string.Empty;
                
                if (infoJson.ContainsKey("gen")) {
                    int gen = infoJson.Value<int>("gen");
                    if (gen >= 2) generation = BackendVersion_Local.V2;
                } else if (!string.IsNullOrEmpty(app) && (app.StartsWith("Pro") || app.StartsWith("Plus"))) {
                    generation = BackendVersion_Local.V2;
                }

                string deviceType = infoJson.Value<string>("type") ?? (string.IsNullOrEmpty(app) ? "Shelly" : app);

                // 2. Determine Channels
                if (generation == BackendVersion_Local.V1) {
                    var settingsResponse = await httpClient.GetAsync($"{baseUrl}/settings", token);
                    settingsResponse.EnsureSuccessStatusCode();
                    var settingsContent = await settingsResponse.Content.ReadAsStringAsync();
                    var settingsJson = JObject.Parse(settingsContent);

                    if (settingsJson.TryGetValue("relays", out var relaysToken) && relaysToken is JArray relaysArray) {
                        for (int i = 0; i < relaysArray.Count; i++) {
                            discovered.Add(CreateDiscovered(ipAddress, username, password, generation, i, $"{deviceType} - Ch {i}"));
                        }
                    } else {
                        discovered.Add(CreateDiscovered(ipAddress, username, password, generation, 0, $"{deviceType} - Ch 0"));
                    }
                } 
                else {
                    var statusResponse = await httpClient.GetAsync($"{baseUrl}/rpc/Shelly.GetStatus", token);
                    statusResponse.EnsureSuccessStatusCode();
                    var statusContent = await statusResponse.Content.ReadAsStringAsync();
                    var statusJson = JObject.Parse(statusContent);

                    int i = 0;
                    while (statusJson.ContainsKey($"switch:{i}")) {
                         discovered.Add(CreateDiscovered(ipAddress, username, password, generation, i, $"{deviceType} - Ch {i}"));
                         i++;
                    }

                    if (discovered.Count == 0) {
                        discovered.Add(CreateDiscovered(ipAddress, username, password, generation, 0, $"{deviceType} - Ch 0"));
                    }
                }

            } catch (Exception ex) {
                Logger.Error($"ShellyScanner: Failed to scan {ipAddress}. {ex.Message}");
                throw;
            }

            return discovered;
        }

        private DiscoveredSwitch CreateDiscovered(string ip, string user, string pass, BackendVersion_Local version, int channel, string defaultName) {
            return new DiscoveredSwitch {
                IpAddress = ip,
                Username = user,
                Password = pass,
                Channel = channel,
                DefaultName = defaultName,
                ProviderType = version == BackendVersion_Local.V1 ? "Shelly" : "ShellyGen2"
            };
        }

        private enum BackendVersion_Local { V1, V2 }
    }

    /// <summary>
    /// Explicit export for Shelly Gen 2/3 provider so the scanner is found when ShellyGen2 is selected in the UI.
    /// </summary>
    [Export(typeof(ISmartSwitchScanner))]
    [ExportMetadata("ProviderId", "ShellyGen2")]
    public class ShellyGen2Scanner : ShellyScanner { }
}
