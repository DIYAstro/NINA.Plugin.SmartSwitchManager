using CommunityToolkit.Mvvm.Input;
using NINA.Plugin.Interfaces;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Backends;
using NINA.Plugin.SmartSwitchManager.Backends;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using NINA.WPF.Base.ViewModel;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NINA.Plugin.SmartSwitchManager.Dockable {

    /// <summary>
    /// ViewModel for the Dockable Panel in the Imaging tab.
    /// Lists all configured Smart Switches and allows manual toggling
    /// directly from the UI, bypassing the single-equipment Switch limit.
    /// </summary>
    [Export(typeof(IDockableVM))]
    public class SmartSwitchDockableVM : DockableVM, IDisposable {

        public ObservableCollection<DockableSwitchItemVM> Switches { get; } = new ObservableCollection<DockableSwitchItemVM>();

        private DockableSwitchItemVM selectedSwitch;
        public DockableSwitchItemVM SelectedSwitch {
            get => selectedSwitch;
            set {
                if (selectedSwitch != value) {
                    selectedSwitch = value;
                    RaisePropertyChanged(nameof(SelectedSwitch));
                }
            }
        }

        private System.Windows.Threading.DispatcherTimer pollTimer;

        [ImportingConstructor]
        public SmartSwitchDockableVM(IProfileService profileService) : base(profileService) {
            Title = "Smart Switches";
            try {
                var dict = new System.Windows.ResourceDictionary();
                dict.Source = new Uri("NINA.Plugin.SmartSwitchManager;component/Dockable/SmartSwitchDockable.xaml", UriKind.RelativeOrAbsolute);
                if (dict.Contains("NINA.Plugin.SmartSwitchManager_PowerSVG")) {
                    ImageGeometry = (System.Windows.Media.GeometryGroup)dict["NINA.Plugin.SmartSwitchManager_PowerSVG"];
                    ImageGeometry.Freeze();
                }
            } catch { }

            // Guard: Plugin.Instance may be null if MEF loads DockableVM before SmartSwitchPlugin
            if (SmartSwitchPlugin.Instance != null) {
                SmartSwitchPlugin.Instance.PropertyChanged += Plugin_PropertyChanged;
                SmartSwitchPlugin.Instance.SmartSwitchConfigs.CollectionChanged += SmartSwitchConfigs_CollectionChanged;

                try {
                    ReloadSwitches();
                } catch (Exception ex) {
                    NINA.Core.Utility.Logger.Error($"SmartSwitchDockableVM: Critical error during ReloadSwitches: {ex.Message}");
                }
            } else {
                NINA.Core.Utility.Logger.Warning("SmartSwitchDockableVM: Plugin.Instance is null during constructor. Switches will not be loaded.");
            }

            // Setup a robust polling timer using the Main Application Dispatcher
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                pollTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, System.Windows.Application.Current.Dispatcher);
                pollTimer.Interval = TimeSpan.FromSeconds(5);
                pollTimer.Tick += PollTimer_Tick;
                pollTimer.Start();
            });
        }

        private void SmartSwitchConfigs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            try {
                ReloadSwitches();
            } catch (Exception ex) {
                NINA.Core.Utility.Logger.Error($"SmartSwitchDockableVM: CollectionChanged reload failed: {ex.Message}");
            }
        }

        private void Plugin_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            try {
                if (e.PropertyName == nameof(SmartSwitchPlugin.SmartSwitchConfigs)) {
                    // Always unsubscribe before subscribing to avoid duplicate handlers across profile changes
                    SmartSwitchPlugin.Instance.SmartSwitchConfigs.CollectionChanged -= SmartSwitchConfigs_CollectionChanged;
                    SmartSwitchPlugin.Instance.SmartSwitchConfigs.CollectionChanged += SmartSwitchConfigs_CollectionChanged;
                    ReloadSwitches();
                }
            } catch (Exception ex) {
                NINA.Core.Utility.Logger.Error($"SmartSwitchDockableVM: Plugin_PropertyChanged failed: {ex.Message}");
            }
        }

        private void ReloadSwitches() {
            // Unsubscribe existing
            foreach (var existing in Switches) {
                existing.IsSelectedChanged -= Item_IsSelectedChanged;
                existing.Dispose();
            }

            Switches.Clear();

            // Load new from singleton config
            foreach (var cfg in SmartSwitchPlugin.Instance.SmartSwitchConfigs) {
                var vm = new DockableSwitchItemVM(cfg);
                vm.IsSelectedChanged += Item_IsSelectedChanged;
                Switches.Add(vm);
            }

            if (Switches.Any()) {
                SelectedSwitch = Switches.First();
            } else {
                SelectedSwitch = null;
            }
        }

        private async void PollTimer_Tick(object sender, EventArgs e) {
            // async void — MUST catch all exceptions as they'd crash the Dispatcher otherwise
            try {
                var tasks = Switches.Select(s => s.SoftPollAsync());
                await Task.WhenAll(tasks);
            } catch (Exception ex) {
                NINA.Core.Utility.Logger.Warning($"SmartSwitchDockableVM: PollTimer_Tick error: {ex.Message}");
            }
        }

        private void Item_IsSelectedChanged(object sender, EventArgs e) {
            // Optional: Handle if need be
        }

        public void Dispose() {
            pollTimer?.Stop();
            if (SmartSwitchPlugin.Instance != null) {
                SmartSwitchPlugin.Instance.PropertyChanged -= Plugin_PropertyChanged;
                SmartSwitchPlugin.Instance.SmartSwitchConfigs.CollectionChanged -= SmartSwitchConfigs_CollectionChanged;
            }
            foreach (var s in Switches) {
                s.Dispose();
            }
        }
    }

    /// <summary>
    /// Represents a single switch inside the dockable panel.
    /// </summary>
    public class DockableSwitchItemVM : INotifyPropertyChanged, IDisposable {
        
        public SmartSwitchConfig Config { get; }
        private ISmartSwitchBackend backend;

        public string DisplayName => string.IsNullOrWhiteSpace(Config.Name) ? "Unknown Switch" : Config.Name;
        public string SubText {
            get {
                string host = Config.GetSetting("Host");
                if (string.IsNullOrEmpty(host)) host = Config.GetSetting("IpAddress");
                string channel = Config.GetSetting("Channel");
                if (string.IsNullOrEmpty(channel)) channel = Config.GetSetting("EntityId");
                return $"{host} ({channel})";
            }
        }

        private bool isOn;
        public bool IsOn { 
            get => isOn; 
            set { 
                if (isOn != value) {
                    isOn = value; 
                    RaisePropertyChanged();
                    IsSelectedChanged?.Invoke(this, EventArgs.Empty);
                }
            } 
        }

        private bool isBusy;
        public bool IsBusy { get => isBusy; set { isBusy = value; RaisePropertyChanged(); } }

        public IAsyncRelayCommand ToggleCommand { get; }

        public event EventHandler IsSelectedChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public DockableSwitchItemVM(SmartSwitchConfig config) {
            Config = config;
            Config.PropertyChanged += Config_PropertyChanged;
            InitializeBackend();
            ToggleCommand = new AsyncRelayCommand(() => ToggleAction());
            _ = SoftPollAsync(); // Initial state poll
        }

        private void InitializeBackend() {
            try {
                backend = BackendFactory.Create(Config);
            } catch (Exception) {
                backend = null;
            }
        }

        private void Config_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(SmartSwitchConfig.Name):
                    RaisePropertyChanged(nameof(DisplayName));
                    break;
                case nameof(SmartSwitchConfig.Settings):
                case nameof(SmartSwitchConfig.ProviderType):
                    RaisePropertyChanged(nameof(SubText));
                    InitializeBackend();
                    _ = SoftPollAsync();
                    break;
            }
        }


        public void Dispose() {
            Config.PropertyChanged -= Config_PropertyChanged;
            backend?.Dispose();
        }

        private async Task ToggleAction() {
            if (backend == null) return;
            IsBusy = true;
            try {
                if (IsOn) {
                    await backend.TurnOffAsync();
                } else {
                    await backend.TurnOnAsync();
                }
            } catch (Exception) {
                // Silently ignore or log error
            } finally {
                // Read back real state
                await SoftPollAsync();
                IsBusy = false;
            }
        }

        public async Task SoftPollAsync() {
            if (backend == null) return;
            try {
                bool state = await backend.GetStateAsync();
                
                // Set backing field directly to avoid triggering Toggle if just binding issue,
                // but we need to notify the UI to update the ToggleButton.
                if (isOn != state) {
                    isOn = state;
                    RaisePropertyChanged(nameof(IsOn));
                }
            } catch {
                // Ignore, switch might be offline
            }
        }

        private void RaisePropertyChanged([CallerMemberName] string prop = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
