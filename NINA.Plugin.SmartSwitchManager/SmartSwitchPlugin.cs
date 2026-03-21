using CommunityToolkit.Mvvm.Input;
using Logger = NINA.Core.Utility.Logger;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using NINA.Plugin.SmartSwitchManager.Core.Backends;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NINA.Plugin.SmartSwitchManager {

    /// <summary>
    /// Main plugin class. Exports the IPluginManifest for N.I.N.A. plugin discovery.
    /// Acts as the DataContext for the Options.xaml DataTemplate.
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class SmartSwitchPlugin : PluginBase, INotifyPropertyChanged {
        public static SmartSwitchPlugin Instance { get; private set; }

        private readonly IPluginOptionsAccessor pluginSettings;
        private readonly IProfileService profileService;
        private System.Threading.CancellationTokenSource scanCts;

        [ImportingConstructor]
        public SmartSwitchPlugin(IProfileService profileService) {
            Instance = this;
            this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
            this.profileService = profileService;

            // React on profile changes
            profileService.ProfileChanged += ProfileService_ProfileChanged;

            // Load switches from current profile
            LoadFromProfile();

            // Initialize MEF discovery
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string pluginDir = System.IO.Path.GetDirectoryName(assemblyPath);
            BackendRegistry.Instance.Initialize(pluginDir);

            // Commands for the UI
            AddSwitchCommand = new RelayCommand(() => AddSwitchAction());
            RemoveSwitchCommand = new RelayCommand<SmartSwitchConfig>(c => RemoveSwitchAction(c));
            OpenScannerCommand = new RelayCommand<SmartSwitchConfig>(c => OpenScannerAction(c));
            ScanCommand = new AsyncRelayCommand<SmartSwitchConfig>(c => ScanForDevicesAction(c));
            CancelScanCommand = new RelayCommand(() => CancelScanAction());
            AddSelectedCommand = new RelayCommand<SmartSwitchConfig>(c => AddSelectedSwitchesAction(c));
            CloseScannerCommand = new RelayCommand<SmartSwitchConfig>(c => CloseScannerAction(c));
        }

        public override System.Threading.Tasks.Task Teardown() {
            scanCts?.Cancel();
            profileService.ProfileChanged -= ProfileService_ProfileChanged;
            return base.Teardown();
        }

        // ── Switch Configuration Collection ──

        private ObservableCollection<SmartSwitchConfig> smartSwitchConfigs = new ObservableCollection<SmartSwitchConfig>();

        /// <summary>
        /// The list of configured smart switches, bound to the Options UI.
        /// </summary>
        public ObservableCollection<SmartSwitchConfig> SmartSwitchConfigs {
            get => smartSwitchConfigs;
            set {
                smartSwitchConfigs = value;
                RaisePropertyChanged();
            }
        }

        // ── Commands ──

        /// <summary>
        /// Adds a new switch with default values.
        /// </summary>
        public ICommand AddSwitchCommand { get; }

        /// <summary>
        /// Removes a switch from the collection. CommandParameter = SmartSwitchConfig to remove.
        /// </summary>
        public ICommand RemoveSwitchCommand { get; }

        public ICommand OpenScannerCommand { get; }
        public IAsyncRelayCommand<SmartSwitchConfig> ScanCommand { get; }
        public ICommand CancelScanCommand { get; }
        public ICommand AddSelectedCommand { get; }
        public ICommand CloseScannerCommand { get; }

        // ── Scanner Properties ──
        
        public IEnumerable<Core.IBackendMetadata> DiscoveredProviders => 
            BackendRegistry.Instance.AvailableProviders;

        private string selectedProviderType = "Shelly";
        public string SelectedProviderType {
            get => selectedProviderType;
            set { selectedProviderType = value; RaisePropertyChanged(); }
        }

        private string scanIpAddress = string.Empty;
        public string ScanIpAddress { get => scanIpAddress; set { scanIpAddress = value; RaisePropertyChanged(); } }

        private string scanUsername = string.Empty;
        public string ScanUsername { get => scanUsername; set { scanUsername = value; RaisePropertyChanged(); } }

        private string scanPassword = string.Empty;
        public string ScanPassword { get => scanPassword; set { scanPassword = value; RaisePropertyChanged(); } }

        private bool isScanning;
        public bool IsScanning { get => isScanning; set { isScanning = value; RaisePropertyChanged(); } }

        private string scanResultMessage = string.Empty;
        public string ScanResultMessage { get => scanResultMessage; set { scanResultMessage = value; RaisePropertyChanged(); } }

        private int timeoutSeconds = 10;
        public int TimeoutSeconds {
            get => timeoutSeconds;
            set {
                timeoutSeconds = Math.Max(1, Math.Min(60, value));
                RaisePropertyChanged();
                SmartSwitchHttpClient.TimeoutSeconds = timeoutSeconds;
                SmartSwitchOptions.SaveTimeout(pluginSettings, timeoutSeconds);
            }
        }

        public ObservableCollection<DiscoveredSwitchViewModel> DiscoveredSwitches { get; } = new ObservableCollection<DiscoveredSwitchViewModel>();

        // ── Methods ──

        private void OpenScannerAction(SmartSwitchConfig config) {
            if (config == null) return;
            try {
                foreach (var c in SmartSwitchConfigs) c.IsScannerOpen = false;
                config.IsScannerOpen = true;
                ScanIpAddress = config.GetSetting("Host");
                if (string.IsNullOrEmpty(ScanIpAddress)) ScanIpAddress = config.GetSetting("IpAddress");
                DiscoveredSwitches.Clear();
                ScanResultMessage = string.Empty;
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchPlugin: OpenScannerAction failed: {ex.Message}");
            }
        }

        private void CloseScannerAction(SmartSwitchConfig config) {
            if (config == null) return;
            try {
                scanCts?.Cancel();
                config.IsScannerOpen = false;
                ScanResultMessage = string.Empty;
                DiscoveredSwitches.Clear();
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchPlugin: CloseScannerAction failed: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ScanForDevicesAction(SmartSwitchConfig targetConfig) {
            if (targetConfig == null) {
                ScanResultMessage = "No active configuration to scan for.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ScanIpAddress)) {
                ScanResultMessage = "Please enter an IP address.";
                return;
            }

            IsScanning = true;
            ScanResultMessage = "Scanning device...";
            DiscoveredSwitches.Clear();

            // Cancel any previous scan
            scanCts?.Cancel();
            scanCts = new System.Threading.CancellationTokenSource();
            
            try {
                var scanner = BackendRegistry.Instance.GetScanner(targetConfig.ProviderType);
                if (scanner == null) {
                    ScanResultMessage = $"Provider '{targetConfig.ProviderType}' does not support network scanning. Please enter the configuration manually.";
                    return;
                }

                var results = await scanner.ScanAsync(ScanIpAddress, ScanUsername, ScanPassword, scanCts.Token);
                foreach (var s in results) {
                    DiscoveredSwitches.Add(new DiscoveredSwitchViewModel(s));
                }
                ScanResultMessage = $"Successfully found {results.Count()} channel(s)!";
            } catch (System.OperationCanceledException) {
                ScanResultMessage = "Scan cancelled.";
            } catch (Exception ex) {
                ScanResultMessage = $"Error: {ex.Message}";
            } finally {
                IsScanning = false;
            }
        }

        private void CancelScanAction() {
            scanCts?.Cancel();
        }

        private void AddSelectedSwitchesAction(SmartSwitchConfig targetConfig) {
            if (targetConfig == null) return;
            try {
                var selected = DiscoveredSwitches.Where(x => x.IsSelected).ToList();
                if (selected.Count == 0) return;

                var first = selected.First();
                targetConfig.Name = first.Name;
                targetConfig.SetSetting("Host", first.SwitchInfo.IpAddress);
                targetConfig.SetSetting("Username", first.SwitchInfo.Username);
                targetConfig.SetSetting("Password", first.SwitchInfo.Password);
                targetConfig.SetSetting("Channel", first.SwitchInfo.Channel.ToString());
                targetConfig.SetSetting("EntityId", first.SwitchInfo.ExternalEntityId);
                targetConfig.SetSetting("Token", first.SwitchInfo.ExternalToken);
                targetConfig.IsScannerOpen = false;

                int targetIndex = SmartSwitchConfigs.IndexOf(targetConfig);
                for (int i = 1; i < selected.Count; i++) {
                    var item = selected[i];
                    var newConfig = new SmartSwitchConfig {
                        Name = item.Name,
                        ProviderType = targetConfig.ProviderType
                    };
                    newConfig.SetSetting("Host", item.SwitchInfo.IpAddress);
                    newConfig.SetSetting("Username", item.SwitchInfo.Username);
                    newConfig.SetSetting("Password", item.SwitchInfo.Password);
                    newConfig.SetSetting("Channel", item.SwitchInfo.Channel.ToString());
                    newConfig.SetSetting("EntityId", item.SwitchInfo.ExternalEntityId);
                    newConfig.SetSetting("Token", item.SwitchInfo.ExternalToken);
                    SmartSwitchConfigs.Insert(targetIndex + i, newConfig);
                }

                SaveToProfile();
                DiscoveredSwitches.Clear();
                ScanResultMessage = string.Empty;
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchPlugin: AddSelectedSwitchesAction failed: {ex.Message}");
            }
        }

        private void AddSwitchAction() {
            try {
                SmartSwitchConfigs.Add(new SmartSwitchConfig());
                SaveToProfile();
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchPlugin: AddSwitchAction failed: {ex.Message}");
            }
        }

        private void RemoveSwitchAction(SmartSwitchConfig config) {
            if (config == null) return;
            try {
                SmartSwitchConfigs.Remove(config);
                SaveToProfile();
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchPlugin: RemoveSwitchAction failed: {ex.Message}");
            }
        }

        // ── Persistence ──

        private void LoadFromProfile() {
            // Unsubscribe from the old collection before replacing it to prevent memory leaks
            if (smartSwitchConfigs != null) {
                smartSwitchConfigs.CollectionChanged -= SmartSwitchConfigs_CollectionChanged;
                foreach (var config in smartSwitchConfigs) {
                    config.PropertyChanged -= Config_PropertyChanged;
                }
            }

            var configs = SmartSwitchOptions.LoadSwitches(pluginSettings);
            SmartSwitchConfigs = new ObservableCollection<SmartSwitchConfig>(configs);
            timeoutSeconds = SmartSwitchOptions.LoadTimeout(pluginSettings);
            SmartSwitchHttpClient.TimeoutSeconds = timeoutSeconds;

            // Subscribe to property changes for auto-save
            foreach (var config in SmartSwitchConfigs) {
                config.PropertyChanged += Config_PropertyChanged;
            }
            SmartSwitchConfigs.CollectionChanged += SmartSwitchConfigs_CollectionChanged;
        }

        private void SaveToProfile() {
            SmartSwitchOptions.SaveSwitches(pluginSettings, SmartSwitchConfigs.ToList());
        }

        private void SmartSwitchConfigs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            // Subscribe to new items
            if (e.NewItems != null) {
                foreach (SmartSwitchConfig item in e.NewItems) {
                    item.PropertyChanged += Config_PropertyChanged;
                }
            }
            // Unsubscribe from removed items
            if (e.OldItems != null) {
                foreach (SmartSwitchConfig item in e.OldItems) {
                    item.PropertyChanged -= Config_PropertyChanged;
                }
            }
        }

        private void Config_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            try {
                if (e.PropertyName == nameof(SmartSwitchConfig.ProviderType)) {
                    var config = sender as SmartSwitchConfig;
                    if (config != null && config.IsScannerOpen) CloseScannerAction(config);
                }
                SaveToProfile();
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchPlugin: Config_PropertyChanged failed: {ex.Message}");
            }
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e) {
            try {
                LoadFromProfile();
                RaisePropertyChanged(nameof(SmartSwitchConfigs));
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchPlugin: ProfileService_ProfileChanged failed: {ex.Message}");
            }
        }

        // ── INotifyPropertyChanged ──

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DiscoveredSwitchViewModel : INotifyPropertyChanged {
        public DiscoveredSwitch SwitchInfo { get; }
        
        private bool isSelected = true;
        public bool IsSelected { 
            get => isSelected; 
            set { isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        private string name;
        public string Name {
            get => name;
            set { name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }

        public DiscoveredSwitchViewModel(DiscoveredSwitch info) {
            SwitchInfo = info;
            Name = info.DefaultName;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
