using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Plugin.SmartSwitchManager.Core.Backends;
using NINA.Plugin.SmartSwitchManager.Backends;
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.SmartSwitchManager.SequenceItems {

    [ExportMetadata("Name", "Toggle Smart Switch")]
    [ExportMetadata("Description", "Turns a configured Smart Switch On or Off. The switch does not need to be actively connected in the Equipment tab.")]
    [ExportMetadata("Icon", "SvgPower")] 
    [ExportMetadata("Category", "Smart Switch Manager")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SmartSwitchInstruction : SequenceItem {

        [ImportingConstructor]
        public SmartSwitchInstruction() {
            TurnOn = true;
        }

        public SmartSwitchInstruction(SmartSwitchInstruction copyMe) : base() {
            CopyMetaData(copyMe);
            SelectedSwitchId = copyMe.SelectedSwitchId;
            TurnOn = copyMe.TurnOn;
            DelaySeconds = copyMe.DelaySeconds;
        }

        private Guid selectedSwitchId;
        [JsonProperty]
        public Guid SelectedSwitchId {
            get => selectedSwitchId;
            set {
                selectedSwitchId = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SwitchName));
                RaisePropertyChanged(nameof(IsDelaySupported));
            }
        }

        private bool turnOn;
        [JsonProperty]
        public bool TurnOn {
            get => turnOn;
            set {
                turnOn = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(TargetStateText));
            }
        }

        private int delaySeconds = 0;
        [JsonProperty]
        public int DelaySeconds {
            get => delaySeconds;
            set {
                delaySeconds = value;
                RaisePropertyChanged();
            }
        }

        public bool IsDelaySupported {
            get {
                var config = AvailableSwitches.FirstOrDefault(s => s.Id == SelectedSwitchId);
                return BackendFactory.SupportsHardwareTimer(config);
            }
        }

        // Exposing as IList for WPF binding stability
        public IList<SmartSwitchConfig> AvailableSwitches {
            get {
                if (SmartSwitchPlugin.Instance == null) return new List<SmartSwitchConfig>();
                return SmartSwitchPlugin.Instance.SmartSwitchConfigs;
            }
        }

        public string SwitchName => AvailableSwitches.FirstOrDefault(s => s.Id == SelectedSwitchId)?.Name ?? "Unknown Switch";
        
        public string TargetStateText => TurnOn ? "ON" : "OFF";

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            var switches = AvailableSwitches.ToList();
            var config = switches.FirstOrDefault(s => s.Id == SelectedSwitchId);

            if (config == null) {
                Logger.Error($"SmartSwitchInstruction: Switch with ID {SelectedSwitchId} not found in profile.");
                throw new InvalidOperationException($"Smart Switch with ID {SelectedSwitchId} is not configured.");
            }

            // NOTE: A new backend instance is created per execution run.
            // This is intentional – the Sequencer instruction is stateless.
            // Backends implement IDisposable, so they must be properly disposed after execution.
            // The shared HttpClient (SmartSwitchHttpClient.Instance) is reused internally.
            ISmartSwitchBackend backend = null;
            try {
                Logger.Info($"SmartSwitchInstruction: Turning {(TurnOn ? "ON" : "OFF")} switch '{config.Name}' at {config.GetSetting("Host")} (Delay={DelaySeconds}s)");
                backend = BackendFactory.Create(config);
                await backend.SetStateAsync(TurnOn, DelaySeconds);
                Logger.Info($"SmartSwitchInstruction: Successfully toggled switch '{config.Name}'.");
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchInstruction: Failed to toggle switch '{config.Name}': {ex.Message}");
                throw;
            } finally {
                backend?.Dispose();
                backend = null; // Allow GC to collect
            }
        }

        public override object Clone() {
            return new SmartSwitchInstruction(this);
        }

        public override string ToString() {
            return $"Toggle SmartSwitch '{SwitchName}' -> {TargetStateText}";
        }
    }
}
