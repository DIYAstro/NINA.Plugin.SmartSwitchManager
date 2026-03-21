using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.Plugin.SmartSwitchManager.Dockable {
    // IMPORTANT: N.I.N.A templates must export ResourceDictionary via MEF
    [Export(typeof(ResourceDictionary))]
    public partial class SmartSwitchDockable : ResourceDictionary {
        public SmartSwitchDockable() {
            InitializeComponent();
        }
    }
}
