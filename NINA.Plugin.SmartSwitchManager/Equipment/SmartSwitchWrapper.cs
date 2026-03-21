using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Backends;
using NINA.Plugin.SmartSwitchManager.Backends;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.Equipment {

    /// <summary>
    /// Wraps a single SmartSwitchConfig + ISmartSwitchBackend as a N.I.N.A. IWritableSwitch.
    /// This allows each configured smart switch to appear as a native switch
    /// in the N.I.N.A. Equipment → Switch panel.
    /// </summary>
    public class SmartSwitchWrapper : IWritableSwitch, INotifyPropertyChanged, IDisposable {
        private readonly SmartSwitchConfig config;
        private readonly ISmartSwitchBackend backend;

        private double currentValue = 0.0;
        private double targetValue = 0.0;

        private bool? _cachedState = null;
        private readonly object _stateLock = new object();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public SmartSwitchWrapper(short index, SmartSwitchConfig config, ISmartSwitchBackend backend) {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            Id = index;
            _ = PollingLoop(_cts.Token);
        }

        private async Task PollingLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try {
                    bool result = await backend.GetStateAsync();
                    lock (_stateLock) {
                        _cachedState = result;
                    }
                } catch (Exception) {
                    lock (_stateLock) {
                        _cachedState = null;
                    }
                }

                try {
                    await Task.Delay(5000, token);
                } catch (TaskCanceledException) {
                    break;
                }
            }
        }

        public void Dispose() {
            try {
                _cts.Cancel();
                _cts.Dispose();
            } catch { }
            backend?.Dispose();
        }

        // ── ISwitch ──

        public short Id { get; }

        public string Name => config.Name;

        public string Description => $"{config.ProviderType} switch at {config.GetSetting("Host")}";

        public double Value {
            get => currentValue;
            private set {
                if (Math.Abs(currentValue - value) > 0.001) {
                    currentValue = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the current switch state from the cache and updates Value.
        /// Called periodically by N.I.N.A. while the switch hub is connected.
        /// </summary>
        public bool Poll() {
            try {
                bool? state;
                lock (_stateLock) {
                    state = _cachedState;
                }
                
                if (state.HasValue) {
                    Value = state.Value ? 1.0 : 0.0;
                }
                return true;
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchWrapper: Poll failed for '{Name}': {ex.Message}");
                return true; // Return true to keep polling; false would disconnect
            }
        }

        // ── IWritableSwitch ──

        public double Maximum => 1.0;

        public double Minimum => 0.0;

        public double StepSize => 1.0;

        public double TargetValue {
            get => targetValue;
            set {
                targetValue = value >= 0.5 ? 1.0 : 0.0;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Applies the TargetValue by calling the backend's TurnOn/TurnOff.
        /// N.I.N.A. calls this after the user sets TargetValue.
        /// </summary>
        public void SetValue() {
            try {
                bool targetState = TargetValue >= 0.5;

                // Execute switch command and wait for confirmation (blocking the sequence thread)
                Task.Run(async () => {
                    if (targetState) {
                        await backend.TurnOnAsync();
                    } else {
                        await backend.TurnOffAsync();
                    }

                    // Confirmation Loop (max 10 seconds)
                    bool confirmed = false;
                    for (int i = 0; i < 5; i++) {
                        await Task.Delay(2000); // Wait 2s before checking state
                        
                        try {
                            bool actualState = await backend.GetStateAsync();
                            if (actualState == targetState) {
                                confirmed = true;
                                lock (_stateLock) {
                                    _cachedState = targetState;
                                }
                                break;
                            }
                        } catch (Exception ex) {
                            Logger.Warning($"SmartSwitchWrapper: State check failed during SetValue for '{Name}': {ex.Message}");
                        }
                    }

                    if (!confirmed) {
                        // Throw up to N.I.N.A. sequence logic so it triggers sequence error handling
                        throw new Exception($"Timeout or error waiting for switch '{Name}' state confirmation.");
                    }
                }).GetAwaiter().GetResult();

                Value = TargetValue;
                Logger.Info($"SmartSwitchWrapper: Successfully set {Name} to {TargetValue}");
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchWrapper: SetValue failed for '{Name}': {ex.Message}");
                // Re-throw to let NINA handle the timeout/error visual feedback
                throw;
            }
        }

        // ── INotifyPropertyChanged ──

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
