using Newtonsoft.Json;
using NINA.Core.Utility;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.SmartSwitchManager {

    /// <summary>
    /// Handles loading and saving of switch configurations from/to the N.I.N.A. profile.
    /// Uses JSON serialization via PluginOptionsAccessor for structured data persistence.
    /// </summary>
    public static class SmartSwitchOptions {
        private const string SettingsKey = "SmartSwitchConfigs";
        private const string TimeoutKey = "SmartSwitchTimeout";
        private const int DefaultTimeout = 10;

        /// <summary>
        /// Loads the list of switch configurations from the active N.I.N.A. profile.
        /// </summary>
        public static List<SmartSwitchConfig> LoadSwitches(IPluginOptionsAccessor accessor) {
            if (accessor == null) return new List<SmartSwitchConfig>();

            var json = accessor.GetValueString(SettingsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) {
                return new List<SmartSwitchConfig>();
            }

            try {
                return JsonConvert.DeserializeObject<List<SmartSwitchConfig>>(json)
                       ?? new List<SmartSwitchConfig>();
            } catch {
                NINA.Core.Utility.Logger.Error("SmartSwitchOptions: Failed to deserialize switch configs.");
                return new List<SmartSwitchConfig>();
            }
        }

        /// <summary>
        /// Saves the list of switch configurations to the active N.I.N.A. profile.
        /// </summary>
        public static void SaveSwitches(IPluginOptionsAccessor accessor, IList<SmartSwitchConfig> switches) {
            if (accessor == null) return;
            try {
                var json = JsonConvert.SerializeObject(switches, Formatting.None);
                accessor.SetValueString(SettingsKey, json);
            } catch (Exception ex) {
                Logger.Error($"SmartSwitchOptions: Failed to serialize switch configs: {ex.Message}");
            }
        }
        public static int LoadTimeout(IPluginOptionsAccessor accessor) {
            if (accessor == null) return DefaultTimeout;
            return accessor.GetValueInt32(TimeoutKey, DefaultTimeout);
        }

        public static void SaveTimeout(IPluginOptionsAccessor accessor, int timeout) {
            if (accessor == null) return;
            accessor.SetValueInt32(TimeoutKey, timeout);
        }
    }
}
