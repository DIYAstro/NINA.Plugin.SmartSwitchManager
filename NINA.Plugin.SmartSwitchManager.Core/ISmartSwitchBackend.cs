using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Core {

    /// <summary>
    /// Metadata interface for smart switch backend providers.
    /// Used by MEF to discover provider names and IDs without loading the assembly.
    /// </summary>
    public interface IBackendMetadata {
        /// <summary>
        /// The unique ID of the provider (e.g. "Shelly", "Tasmota").
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// The display name of the provider.
        /// </summary>
        string DisplayName { get; }
 
        /// <summary>
        /// Whether the backend supports a hardware-based timer (delay parameter for SetState).
        /// </summary>
        bool SupportsHardwareTimer { get; }
        
        /// <summary>
        /// Whether the backend supports network/device scanning.
        /// </summary>
        bool SupportsScanning { get; }
    }

    /// <summary>
    /// Attribute to define backend metadata.
    /// </summary>
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class)]
    public class ExportBackendAttribute : ExportAttribute {
        public string ProviderId { get; }
        public string DisplayName { get; }
 
        public bool SupportsHardwareTimer { get; set; } = false;
        public bool SupportsScanning { get; set; } = false;
 
        public ExportBackendAttribute(string providerId, string displayName)
            : base(typeof(ISmartSwitchBackend)) {
            ProviderId = providerId;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// Core interface for all smart switch backends.
    /// </summary>
    public interface ISmartSwitchBackend : IDisposable {
        /// <summary>
        /// Initializes the backend with the given configuration.
        /// </summary>
        void Initialize(Models.SmartSwitchConfig config);

        bool SupportsHardwareTimer { get; }

        /// <summary>
        /// List of configuration fields required by this provider.
        /// </summary>
        System.Collections.Generic.IEnumerable<Models.ConfigFieldDescriptor> ConfigFields { get; }
 
        Task<bool> GetStateAsync();
        Task TurnOnAsync();
        Task TurnOffAsync();
        Task SetStateAsync(bool targetState, int delaySeconds = 0);
    }
}
