namespace NINA.Plugin.SmartSwitchManager.Core.Models {

    /// <summary>
    /// Represents a switch found during a network scan.
    /// Shared between all provider scanners.
    /// </summary>
    public class DiscoveredSwitch {
        public string IpAddress { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int Channel { get; set; }
        public string ExternalEntityId { get; set; } = string.Empty;
        public string ExternalToken { get; set; } = string.Empty;
        public string DefaultName { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
    }
}
