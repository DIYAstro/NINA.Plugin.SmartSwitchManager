using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using NINA.Plugin.SmartSwitchManager.Core.Backends;
using System;

namespace NINA.Plugin.SmartSwitchManager.Backends {

    /// <summary>
    /// Factory that creates the appropriate ISmartSwitchBackend based on a switch configuration.
    /// To add a new provider, add a case to the switch statement below.
    /// See ADD_NEW_PROVIDER.md for the full checklist.
    /// </summary>
    public static class BackendFactory {

        /// <summary>
        /// Creates a backend instance for the given switch configuration.
        /// Uses BackendRegistry.CreateBackendInstance() to avoid double instantiation.
        /// </summary>
        public static ISmartSwitchBackend Create(SmartSwitchConfig config) {
            if (config == null) throw new ArgumentNullException(nameof(config));

            string resolvedId = BackendRegistry.Instance.GetResolvedProviderId(config);
            var backend = BackendRegistry.Instance.CreateBackendInstance(resolvedId);

            if (backend == null) {
                throw new NotSupportedException($"Switch provider '{config.ProviderType}' is not available or not loaded.");
            }

            backend.Initialize(config);
            return backend;
        }

        /// <summary>
        /// Checks if the provider described by the configuration supports hardware timers.
        /// </summary>
        public static bool SupportsHardwareTimer(SmartSwitchConfig config) {
            if (config == null) return false;
            return BackendRegistry.Instance.SupportsHardwareTimer(config.ProviderType);
        }
    }
}
