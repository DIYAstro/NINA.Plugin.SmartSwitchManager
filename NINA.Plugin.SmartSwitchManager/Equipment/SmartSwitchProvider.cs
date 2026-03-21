using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;

namespace NINA.Plugin.SmartSwitchManager.Equipment {

    /// <summary>
    /// MEF-exported equipment provider that makes the SmartSwitchHub
    /// available in N.I.N.A.'s Equipment → Switch chooser.
    /// </summary>
    [Export(typeof(IEquipmentProvider))]
    public class SmartSwitchProvider : IEquipmentProvider<ISwitchHub> {
        private readonly IProfileService profileService;

        // Read GUID from assembly attribute to avoid duplication with AssemblyInfo.cs
        private static readonly Guid PluginGuid = Guid.Parse(
            ((GuidAttribute)typeof(SmartSwitchProvider).Assembly
                .GetCustomAttributes(typeof(GuidAttribute), false)[0]).Value);

        [ImportingConstructor]
        public SmartSwitchProvider(IProfileService profileService) {
            this.profileService = profileService;
        }

        public string Name => "Smart Switch Manager";

        public IList<ISwitchHub> GetEquipment() {
            var accessor = new PluginOptionsAccessor(profileService, PluginGuid);
            var hub = new SmartSwitchHub(accessor);
            return new List<ISwitchHub> { hub };
        }
    }
}
