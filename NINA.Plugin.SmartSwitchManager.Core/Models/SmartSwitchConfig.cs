using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using NINA.Plugin.SmartSwitchManager.Core.Backends;

namespace NINA.Plugin.SmartSwitchManager.Core.Models {

    /// <summary>
    /// Configuration for a single smart switch.
    /// </summary>
    public class SmartSwitchConfig : INotifyPropertyChanged {
        private Guid id = Guid.NewGuid();
        private string name = "New Switch";
        private string providerType = "Shelly";
        private System.Collections.Generic.List<ConfigFieldViewModel> configViewModels;

        [Newtonsoft.Json.JsonIgnore]
        public IBackendMetadata ProviderMetadata =>
            BackendRegistry.Instance.AvailableProviders.FirstOrDefault(p => p.ProviderId == ProviderType);

        public Guid Id {
            get => id;
            set { if (id != value) { id = value; OnPropertyChanged(); } }
        }

        public string Name {
            get => name;
            set { if (name != value) { name = value; OnPropertyChanged(); } }
        }

        public string ProviderType {
            get => providerType;
            set {
                if (providerType != value) {
                    providerType = value;
                    OnPropertyChanged();
                    OnProviderTypeChanged();
                }
            }
        }

        private bool expertModeEnabled = false;
        public bool ExpertModeEnabled {
            get => expertModeEnabled;
            set {
                if (expertModeEnabled != value) {
                    expertModeEnabled = value;
                    OnPropertyChanged();
                    if (configViewModels != null) {
                        foreach (var vm in configViewModels) {
                            vm.UpdateVisibility();
                        }
                    }
                }
            }
        }

        // --- Dynamic Configuration ---
        private System.Collections.Generic.Dictionary<string, string> settings = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public System.Collections.Generic.Dictionary<string, string> Settings {
            get => settings;
            set { settings = value; OnPropertyChanged(); }
        }

        public string GetSetting(string key, string defaultValue = "") {
            if (settings != null && settings.TryGetValue(key, out var val)) {
                return val;
            }
            return defaultValue;
        }

        public void SetSetting(string key, string value) {
            if (settings == null) settings = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            // Only update if it actually changed to prevent infinite loops
            if (!settings.TryGetValue(key, out var current) || current != value) {
                settings[key] = value;
                OnPropertyChanged(nameof(Settings));

                // Notify UI immediately if view models are already generated
                if (configViewModels != null) {
                    var vm = configViewModels.FirstOrDefault(v => v.Descriptor.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                    vm?.ExternalUpdate();
                }
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        public System.Collections.Generic.IEnumerable<ConfigFieldViewModel> ConfigViewModels {
            get {
                if (configViewModels == null) {
                    var fields = Backends.BackendRegistry.Instance.GetConfigFields(ProviderType);
                    configViewModels = fields?.Select(f => new ConfigFieldViewModel(f, this)).ToList() 
                                       ?? new System.Collections.Generic.List<ConfigFieldViewModel>();
                }
                return configViewModels;
            }
        }

        // --- Transient UI State (Not Saved) ---
        private bool isScannerOpen = false;

        [Newtonsoft.Json.JsonIgnore]
        public bool IsScannerOpen {
            get => isScannerOpen;
            set { isScannerOpen = value; OnPropertyChanged(); }
        }

        // Trigger ConfigViewModels refresh when ProviderType changes
        private void OnProviderTypeChanged() {
             configViewModels = null; // Invalidate cache
             OnPropertyChanged(nameof(ProviderMetadata));
             OnPropertyChanged(nameof(ConfigViewModels));
             OnPropertyChanged(nameof(HasExpertFields));
        }

        [Newtonsoft.Json.JsonIgnore]
        public bool HasExpertFields => ConfigViewModels.Any(vm => vm.Descriptor.IsExpertOnly);

        [Newtonsoft.Json.JsonIgnore]
        public bool HasErrors => ConfigViewModels.Any(vm => !vm.IsValid);

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        internal void NotifyHasErrorsChanged() {
            OnPropertyChanged(nameof(HasErrors));
        }
    }

    /// <summary>
    /// Wrapper for a configuration field to facilitate WPF binding to a dictionary.
    /// </summary>
    public class ConfigFieldViewModel : INotifyPropertyChanged {
        private readonly SmartSwitchConfig config;
        private readonly ConfigFieldDescriptor descriptor;

        public ConfigFieldDescriptor Descriptor => descriptor;

        public string Value {
            get => config.GetSetting(descriptor.Key, descriptor.DefaultValue);
            set {
                config.SetSetting(descriptor.Key, value);
                ExternalUpdate();
                config.NotifyHasErrorsChanged();
            }
        }

        public bool IsValid => !descriptor.IsRequired || !string.IsNullOrWhiteSpace(Value);

        public bool IsVisible => !descriptor.IsExpertOnly || config.ExpertModeEnabled;

        public ConfigFieldViewModel(ConfigFieldDescriptor descriptor, SmartSwitchConfig config) {
            this.descriptor = descriptor;
            this.config = config;
        }

        public void UpdateVisibility() {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }

        public void ExternalUpdate() {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsValid)));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
