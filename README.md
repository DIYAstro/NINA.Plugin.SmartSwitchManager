# Smart Switch Manager (N.I.N.A. Plugin)

![Logo](NINA.Plugin.SmartSwitchManager/plugin_logo.png)

A modular plugin for [N.I.N.A. (Nighttime Imaging 'N' Astronomy)](https://nighttime-imaging.eu/) that integrates smart home switches into your astrophotography workflow.

## Overview

The Smart Switch Manager allows you to control power relays and IoT switches as native N.I.N.A. equipment. This enables manual control via the Equipment tab and automated power management within the Advanced Sequencer.

### Supported Providers

*   **Shelly**:
    - Support for Gen 1 (HTTP API) and Gen 2/3 (Plus, Pro series via RPC API).
    - **Status**: Tested with Shelly Pro 1 (Gen 2) and Shelly 2.5 (Gen 1).
    - **Authentication**: Supports Basic and Digest authentication.
    - **Hardware Timer**: Supports delayed switching via internal device timers (ideal for PC shutdown workflows).
      - *Status*: Tested with Gen 2/3. Gen 1 implementation is present but currently field-untested for the timer feature.
    - **Device Scanner**: Scans a specific IP address to detect device generation and available relay channels.
*   **Home Assistant**:
    - Controlled via Long-Lived Access Tokens (LLAT) and REST API.
    - **Configuration**: Requires Host URL, Entity ID, and Token.
*   **Tasmota**:
    - Controlled via the HTTP Command API.
    - **Status**: Backend implementation and hardware verification completed.
    - **Hardware Timer**: Supports "One-Shot" timers using Tasmota Rules.
      - Unlike standard Tasmota `PulseTime`, the plugin ensures the timer is automatically reset and disabled after execution, making it safe for sequential power-cycling.
    - **Expert Setting**: `RuleId` (optional). Defaults to `3`. If your Tasmota device already uses `Rule3` for custom scripts, you can set this to `1` or `2` in the device configuration to avoid conflicts.
    - **Scanner**: Implementation for discovering relays via HTTP.
*   **ESPHome**:
    - Integrated via the `web_server` component's REST API.
    - **Configuration**: Manual input of IP Address and Entity ID (e.g., `switch-garden-light`).
    - **Status**: Implemented.

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

## Installation

### Automatic (via Beta Repository)
As the plugin is currently in beta and not yet in the official N.I.N.A. store, you can add this repository to install and receive updates:
1.  Open N.I.N.A.
2.  Go to **Options → General → Plugin Repository**.
3.  Click the **+** button under **Extra Repositories**.
4.  Paste this URL: `https://raw.githubusercontent.com/DIYAstro/NINA.Plugin.SmartSwitchManager/main/manifests.json?`
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
