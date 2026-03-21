using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using NINA.Core.Utility;

namespace NINA.Plugin.SmartSwitchManager.Providers.Template {

    /// <summary>
    /// This is a commented template for creating a new Smart Switch Provider.
    /// 
    /// 1. Create a new C# Class Library project (targeting .NET 8.0-windows).
    /// 2. Reference 'NINA.Plugin.SmartSwitchManager.Core'.
    /// 3. Implement this class and 'ISmartSwitchBackend'.
    /// 4. Decorate with [ExportBackend].
    /// </summary>
    [ExportBackend("TemplateProvider", "Template / Mock Provider", SupportsScanning = false)]
    public class TemplateBackend : ISmartSwitchBackend {

        // Internal state
        private string host;
        private int port;
        private string password;
        private bool useSsl;

        /// <summary>
        /// Defines the configuration fields shown in the N.I.N.A. options UI.
        /// These are dynamically rendered based on the types provided.
        /// </summary>
        public IEnumerable<ConfigFieldDescriptor> ConfigFields {
            get {
                // Key: The ID used in the settings dictionary.
                // Label: What the user sees in the UI.
                // Type: Text, Number, Password, or Boolean.
                // IsRequired: Validation flag.
                // DefaultValue: Optional initial value.

                yield return new ConfigFieldDescriptor("Host", "IP/Host", ConfigFieldType.Text, isRequired: true);
                yield return new ConfigFieldDescriptor("Port", "Port", ConfigFieldType.Number, isRequired: false, defaultValue: "80");
                yield return new ConfigFieldDescriptor("Password", "Secret Key", ConfigFieldType.Password, isRequired: false);
                yield return new ConfigFieldDescriptor("UseSSL", "Use HTTPS", ConfigFieldType.Boolean, isRequired: false, defaultValue: "False");

                // Expert Fields: Set IsExpertOnly = true to hide them by default.
                // If the provider has at least one expert field, N.I.N.A. will show an "Expert Mode" toggle.
                yield return new ConfigFieldDescriptor("CustomTimeout", "API Timeout", ConfigFieldType.Number) { IsExpertOnly = true, DefaultValue = "10" };
            }
        }

        /// <summary>
        /// Hardware capability hint. If true, N.I.N.A. can send a 'delay' to 'SetStateAsync'.
        /// Usually used for 'Turn on for 5 seconds' features in hardware.
        /// </summary>
        public bool SupportsHardwareTimer => false;

        /// <summary>
        /// Initialization is called when N.I.N.A. loads the plugin or when settings change.
        /// Load your configuration here.
        /// </summary>
        public void Initialize(SmartSwitchConfig config) {
            this.host = config.GetSetting("Host");
            this.password = config.GetSetting("Password");
            
            // For numbers and booleans, parsing is required since everything is stored as string.
            if (!int.TryParse(config.GetSetting("Port", "80"), out this.port)) {
                this.port = 80;
            }

            if (!bool.TryParse(config.GetSetting("UseSSL", "False"), out this.useSsl)) {
                this.useSsl = false;
            }

            // Perform any internal setup (e.g., HttpClient configuration) here.
            Logger.Info($"TemplateBackend: Initialized for {host}:{port}");
        }

        /// <summary>
        /// Queries the device for its current state (ON or OFF).
        /// </summary>
        /// <returns>True if the switch is ON, False if OFF.</returns>
        public async Task<bool> GetStateAsync() {
            try {
                // TODO: Implement your hardware-specific API call here.
                // Example with timeout token:
                // using var cts = SmartSwitchHttpClient.GetCts();
                // var response = await httpClient.GetAsync(..., cts.Token);
                
                await Task.Delay(100); // Simulate network latency
                return true; 
            } catch (Exception ex) {
                Logger.Error($"TemplateBackend: Failed to get state for {host}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Standard Turn On command.
        /// </summary>
        public async Task TurnOnAsync() {
            Logger.Info($"TemplateBackend: Turning ON {host}");
            await SetStateAsync(true);
        }

        /// <summary>
        /// Standard Turn Off command.
        /// </summary>
        public async Task TurnOffAsync() {
            Logger.Info($"TemplateBackend: Turning OFF {host}");
            await SetStateAsync(false);
        }

        /// <summary>
        /// Unified state control with optional hardware timer support.
        /// </summary>
        /// <param name="targetState">True = ON, False = OFF</param>
        /// <param name="delaySeconds">Seconds to wait before automatically toggling back (0 = no timer)</param>
        public async Task SetStateAsync(bool targetState, int delaySeconds = 0) {
            // TODO: Implement your hardware-specific control here.
            
            if (delaySeconds > 0) {
                // If SupportsHardwareTimer is true, send the delay to your API.
                // If false, N.I.N.A. handles the timer in software (this method won't be called with delay > 0).
            }

            await Task.Delay(100); // Simulate network latency
            Logger.Debug($"TemplateBackend: Set {host} to {targetState}");
        }
        /// <summary>
        /// Cleanup resources when the switch is removed or the plugin is unloaded.
        /// </summary>
        public void Dispose() {
            // TODO: Free any resources (like HttpClients or timers) here.
        }
    }
}
