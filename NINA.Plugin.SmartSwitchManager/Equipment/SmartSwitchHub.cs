using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using NINA.Plugin.Interfaces;
using NINA.Plugin.SmartSwitchManager.Core.Backends;
using NINA.Plugin.SmartSwitchManager.Backends;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Equipment {

    /// <summary>
    /// N.I.N.A. ISwitchHub implementation that exposes all configured smart switches
    /// as IWritableSwitch instances. On Connect, it reads the switch list from the
    /// plugin profile settings, creates the appropriate backends, and wraps them.
    /// </summary>
    public class SmartSwitchHub : ISwitchHub, INotifyPropertyChanged {
        private readonly IPluginOptionsAccessor pluginOptions;
        private readonly List<ISwitch> switches = new List<ISwitch>();

        public SmartSwitchHub(IPluginOptionsAccessor pluginOptions) {
            this.pluginOptions = pluginOptions;
        }

        // ── ISwitchHub ──

        public ICollection<ISwitch> Switches => switches;

        // ── IDevice ──

        public bool HasSetupDialog => false;

        public string Id => "SmartSwitchHub";

        public string Name => "Smart Switch Manager";

        public string DisplayName => Name;

        public string Category => "Smart Switch Manager";

        public bool Connected { get; private set; }

        public string Description => "Manages smart home switches (Shelly, HomeAssistant, etc.)";

        public string DriverInfo => "Smart Switch Manager Plugin";

        public string DriverVersion => "1.0";

        public IList<string> SupportedActions => new List<string>();

        /// <summary>
        /// Connects to all configured switches by creating backends and wrapping them.
        /// </summary>
        public Task<bool> Connect(CancellationToken token) {
            try {
                switches.Clear();
 
                var configs = SmartSwitchOptions.LoadSwitches(pluginOptions);
                Logger.Info($"SmartSwitchHub: Connecting with {configs.Count} configured switch(es).");
 
                short index = 0;
                foreach (var config in configs) {
                    try {
                        var backend = BackendFactory.Create(config);
                        var wrapper = new SmartSwitchWrapper(index, config, backend);
                        switches.Add(wrapper);
                        Logger.Info($"SmartSwitchHub: Added switch '{config.Name}' ({config.ProviderType} @ {config.GetSetting("Host")})");
                        index++;
                    } catch (Exception ex) {
                        Logger.Error($"SmartSwitchHub: Failed to create backend for '{config.Name}': {ex.Message}");
                    }
                }
 
                Connected = true;
                RaisePropertyChanged(nameof(Connected));
                RaisePropertyChanged(nameof(Switches));
 
                return Task.FromResult(true);
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchHub: Connect failed: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Disconnects from all switches and clears the list.
        /// </summary>
        public void Disconnect() {
            foreach (var switchItem in switches) {
                if (switchItem is IDisposable disposable) {
                    disposable.Dispose();
                }
            }
            switches.Clear();
            Connected = false;
            RaisePropertyChanged(nameof(Connected));
            RaisePropertyChanged(nameof(Switches));
            Logger.Info("SmartSwitchHub: Disconnected.");
        }

        public void SetupDialog() { }

        public string Action(string actionName, string actionParameters) {
            throw new NotImplementedException();
        }

        public string SendCommandString(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public bool SendCommandBool(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public void SendCommandBlind(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        // ── INotifyPropertyChanged ──

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
