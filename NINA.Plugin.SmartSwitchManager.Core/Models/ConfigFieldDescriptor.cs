using System;

namespace NINA.Plugin.SmartSwitchManager.Core.Models {

    /// <summary>
    /// Supported field types for dynamic configuration UI.
    /// </summary>
    public enum ConfigFieldType {
        Text,
        Password,
        Number,
        Boolean
    }

    /// <summary>
    /// Describes a single configuration field required by a backend provider.
    /// Used to dynamically generate the UI and validate inputs.
    /// </summary>
    public class ConfigFieldDescriptor {
        
        /// <summary>
        /// The unique key used to store the value in the Config dictionary.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The user-facing label for the field.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// The data type of the field.
        /// </summary>
        public ConfigFieldType Type { get; set; } = ConfigFieldType.Text;

        /// <summary>
        /// Optional default value for the field.
        /// </summary>
        public string DefaultValue { get; set; } = string.Empty;

        /// <summary>
        /// Whether the field must be non-empty.
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// Whether this field is only intended for expert users.
        /// </summary>
        public bool IsExpertOnly { get; set; } = false;

        public ConfigFieldDescriptor() { }

        public ConfigFieldDescriptor(string key, string label, ConfigFieldType type = ConfigFieldType.Text, bool isRequired = false, string defaultValue = "") {
            Key = key;
            Label = label;
            Type = type;
            IsRequired = isRequired;
            DefaultValue = defaultValue;
        }
    }
}
