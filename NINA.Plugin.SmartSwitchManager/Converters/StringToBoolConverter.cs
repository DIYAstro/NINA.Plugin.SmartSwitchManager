using System;
using System.Globalization;
using System.Windows.Data;

namespace NINA.Plugin.SmartSwitchManager.Converters {

    public class StringToBoolConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is string s) {
                return bool.TryParse(s, out bool result) && result;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value?.ToString() ?? "False";
        }
    }
}
