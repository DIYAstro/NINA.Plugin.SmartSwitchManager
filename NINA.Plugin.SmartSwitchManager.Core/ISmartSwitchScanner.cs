using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NINA.Plugin.SmartSwitchManager.Core.Models;

namespace NINA.Plugin.SmartSwitchManager.Core {

    /// <summary>
    /// Interface for discovering smart switches on the network.
    /// </summary>
    public interface ISmartSwitchScanner {
        /// <summary>
        /// Performs a scan for devices and their channels.
        /// </summary>
        /// <param name="ipAddress">The IP or hostname to scan.</param>
        /// <param name="user">Optional username for authentication.</param>
        /// <param name="pass">Optional password for authentication.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A collection of discovered switches and their metadata.</returns>
        Task<IEnumerable<DiscoveredSwitch>> ScanAsync(string ipAddress, string user, string pass, CancellationToken token);
    }
}
