using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.Plugin.SmartSwitchManager.SequenceItems {

    /// <summary>
    /// Exports the DataTemplates for the Sequence Items so N.I.N.A. can display them.
    /// </summary>
    [Export(typeof(ResourceDictionary))]
    public partial class SmartSwitchTemplates : ResourceDictionary {
        public SmartSwitchTemplates() {
            InitializeComponent();
        }
    }
}
