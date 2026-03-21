using System.Windows;
using System.Windows.Controls;
using NINA.Plugin.SmartSwitchManager.Core.Models;

namespace NINA.Plugin.SmartSwitchManager.Converters {

    /// <summary>
    /// Selects the appropriate DataTemplate for a dynamic configuration field 
    /// based on its ConfigFieldType.
    /// </summary>
    public class ConfigFieldTemplateSelector : DataTemplateSelector {
        public DataTemplate TextTemplate { get; set; }
        public DataTemplate PasswordTemplate { get; set; }
        public DataTemplate NumberTemplate { get; set; }
        public DataTemplate BooleanTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            ConfigFieldDescriptor descriptor = null;

            if (item is ConfigFieldViewModel vm) {
                descriptor = vm.Descriptor;
            } else if (item is ConfigFieldDescriptor d) {
                descriptor = d;
            }

            if (descriptor != null) {
                switch (descriptor.Type) {
                    case ConfigFieldType.Password:
                        return PasswordTemplate;
                    case ConfigFieldType.Number:
                        return NumberTemplate;
                    case ConfigFieldType.Boolean:
                        return BooleanTemplate;
                    default:
                        return TextTemplate;
                }
            }
            return base.SelectTemplate(item, container);
        }
    }
}
