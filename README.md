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
| **Tasmota** | All (ESP8266/ESP32) | HTTP | Lab Tested | Yes (Rule-based) |
| **Home Assistant** | Entities / Switches | REST | Lab Tested | No |
| **ESPHome** | `web_server` components | REST | Lab Tested | No |

#### Provider Notes

*   **Shelly**: Supports Basic/Digest auth. Hardware timers are ideal for PC shutdown or cooling workflows.
*   **Tasmota**: Uses a verified "One-Shot" rule-based timer. 
    - **Expert Setting**: `RuleId` (1, 2, or 3). Defaults to `3`.
*   **Home Assistant**: Requires a Long-Lived Access Token (LLAT).

### Sequencer Behavior
The **Toggle SmartSwitch** instruction in the Advanced Sequencer has a built-in safety check:
*   **Normal Toggle (Delay = 0s)**: NINA will wait (up to 10s) until the device physically confirms the state change before moving to the next instruction.
*   **Hardware Timer (Delay > 0s)**: NINA will continue immediately once the device acknowledges the command, while the timer runs autonomously on the hardware.

### Features

*   **Equipment Integration**: Appears in the **Equipment → Switch** tab. 
*   **Discovery**: Scanners for Shelly and Tasmota to identify device details and channels at a given IP address.
*   **Sequencer Instructions**:
    *   **Smart Switch Set**: Integration into the N.I.N.A. Advanced Sequencer.
*   **Architecture**: Decoupled backend-provider structure with attribute-based capability discovery.

## Installation

### Automatic (via Beta Repository)
As the plugin is currently in beta and not yet in the official N.I.N.A. store, you can add this repository to install and receive updates:
1.  Open N.I.N.A.
2.  Go to **Options → General → Plugin Repository**.
3.  Click the **+** button under **Extra Repositories**.
4.  Paste this URL: `https://github.com/DIYAstro/NINA.Plugin.SmartSwitchManager/releases/latest/download/manifests.json`
5.  Go to **Plugins → Available** and search for **Smart Switch Manager**.


### Manual
1.  Download the latest release DLL (`NINA.Plugin.SmartSwitchManager.dll`).
2.  Navigate to your N.I.N.A. plugins directory: `%localappdata%\NINA\3\Plugins\`.
3.  Create a folder named `SmartSwitchManager`.
4.  Copy the DLL file into that folder.
5.  Restart N.I.N.A.

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
