# Smart Switch Manager (N.I.N.A. Plugin)

![Logo](NINA.Plugin.SmartSwitchManager/plugin_logo.png)

A modular plugin for [N.I.N.A. (Nighttime Imaging 'N' Astronomy)](https://nighttime-imaging.eu/) that integrates smart home switches into your astrophotography workflow.

## Overview

The Smart Switch Manager allows you to control power relays and IoT switches as native N.I.N.A. equipment. This enables manual control via the Equipment tab and automated power management within the Advanced Sequencer.

### Supported Providers

| Provider | Hardware Generations | API Type | Status | Hardware Timer |
| :--- | :--- | :--- | :--- | :--- |
| **Shelly** | Gen 2 / 3 (Plus, Pro) | RPC | Lab & Field Tested | Yes |
| **Shelly** | Gen 1 | HTTP | Lab Tested | Yes |
| **Tasmota** | All (ESP8266/ESP32) | HTTP | Lab Tested | No |
| **Home Assistant** | Entities / Scripts / Timers | REST | Lab & Field Tested | **Yes (via Scripts)** |
| **ESPHome** | `web_server` components | REST | Lab Tested | No* |

#### Provider Notes

*   **Shelly**: Supports Basic/Digest auth. Hardware timers are ideal for PC shutdown or cooling workflows.
*   **Tasmota**: Reliable sequential request enforcement. See the [Tasmota Documentation](Providers/Tasmota/README.md) for important hardware/template tips.
*   **Home Assistant**: Integration via the standard REST API (Entities) or a dedicated **Script/Timer** backend. The latter uses a blueprint to handle autonomous delays directly on the HA instance. Requires a Long-Lived Access Token (LLAT).
*   **ESPHome**: Native hardware timers are currently not implemented in the plugin. However, autonomous behavior can be achieved by tailoring the device's internal YAML logic (e.g., `delayed_off`).

### Sequencer Behavior
The **Toggle SmartSwitch** instruction in the Advanced Sequencer has a built-in safety check:
*   **Normal Toggle (Delay = 0s)**: NINA will wait (up to 10s) until the device physically confirms the state change before moving to the next instruction.
*   **Hardware Timer (Delay > 0s)**: NINA will continue immediately once the device acknowledges the command, while the timer runs autonomously on the hardware.

### Features

*   **Equipment Integration**: Appears in the **Equipment → Switch** tab. 
*   **Entity Discovery**: Scanners for Shelly and Tasmota to identify available switch entities (channels) at a specified IP address.
*   **Sequencer Instructions**:
    *   **Smart Switch Set**: Integration into the N.I.N.A. Advanced Sequencer.
*   **Architecture**: Decoupled backend-provider structure with attribute-based capability discovery.

## Installation

### Automatic (via N.I.N.A. Plugin Manager)
The Smart Switch Manager is available in the official N.I.N.A. plugin repository!
1.  Open N.I.N.A.
2.  Go to **Plugins → Available** and search for **Smart Switch Manager**.
3.  Click **Install** in the top right corner.
4.  Restart N.I.N.A.

### Manual
1.  Download the latest release ZIP from the GitHub [Releases](https://github.com/DIYAstro-Obs/NINA.Plugin.SmartSwitchManager/releases) page.
2.  Extract the contents into your N.I.N.A. plugins directory: `%localappdata%\NINA\3\Plugins\SmartSwitchManager`.
3.  Restart N.I.N.A.

## Usage

1.  **Configuration**: 
    - Go to **Plugins → Installed → Smart Switch Manager**.
    - Select your provider (e.g., Shelly).
    - Use the **Device Scanner** or **Add Switch Manually** to configure your devices.
    - Set custom names (e.g., "Mount Power", "Dew Heater") and credentials if required.
2.  **Manual Control**:
    - Go to **Equipment → Switch**, select **Smart Switch Manager** and click **Connect**.
    - Toggle your switches directly from the **Switch** panel or the **Imaging** tab (**Smart Switches** window).
3.  **Automation**:
    - In the **Advanced Sequencer**, search for the **Smart Switch Set** instruction.
    - Drag it into your sequence and select the desired switch and state.

## Developer Info

If you want to add support for a new smart switch ecosystem (like Tasmota or Home Assistant), please refer to the [ADD_NEW_PROVIDER.md](ADD_NEW_PROVIDER.md) guide.

### Building from Source
Requires .NET 8.0 SDK.
```powershell
dotnet build NINA.Plugin.SmartSwitchManager.sln -c Release
```

## License
Licensed under the MPL-2.0 License.

## Disclaimer
This software is provided "as is", without warranty of any kind, express or implied. The author and contributors are not responsible for any damage to equipment or data resulting from its use. Use at your own risk.
