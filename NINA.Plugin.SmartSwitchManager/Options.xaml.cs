using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.Plugin.SmartSwitchManager {

    [Export(typeof(ResourceDictionary))]
    partial class Options : ResourceDictionary {

        public Options() {
            InitializeComponent();
        }
    }
}
