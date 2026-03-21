# Provider Implementation Guide

This guide describes how to implement a new hardware provider (backend) for the **NINA.Plugin.SmartSwitchManager**.

## Core Concepts

The plugin uses a dynamic configuration system powered by the Managed Extensibility Framework (MEF). To add a new provider, you must create a class that implements the `ISmartSwitchBackend` interface and decorate it with the `ExportBackend` attribute.

### 1. Project Setup

1.  Create a new C# Class Library project (targeting `net8.0-windows7.0` to match N.I.N.A. 3.x).
2.  Add a reference to `NINA.Plugin.SmartSwitchManager.Core.csproj`.
3.  **Recommended:** Place the project in the `Providers/` folder. It will then automatically inherit shared build settings (like N.I.N.A. library versions) from the root `Directory.Build.props`.
4.  Ensure the output DLL is placed in the `Backends/` folder of the main plugin directory.

### 2. Implementing the Backend

Your class must implement `NINA.Plugin.SmartSwitchManager.Core.ISmartSwitchBackend`, which inherits from `IDisposable`.

#### Mandatory Attribute
Decorate the class with `ExportBackendAttribute`:
- `ProviderId`: A unique string ID (e.g., "MyHardware").
- `DisplayName`: The name shown in the N.I.N.A. options dropdown.
- `SupportsScanning`: (Optional) Set to `true` if you implement a corresponding `ISmartSwitchScanner`.

```csharp
[ExportBackend("MyHardware", "My Hardware Brand", SupportsScanning = false)]
public class MyHardwareBackend : ISmartSwitchBackend {
    // ...
}
```

#### Dynamic Configuration Fields
Implementation of the `ConfigFields` property defines the UI elements shown in the N.I.N.A. options.

```csharp
public IEnumerable<ConfigFieldDescriptor> ConfigFields {
    get {
        yield return new ConfigFieldDescriptor("Host", "IP/Host", ConfigFieldType.Text, true);
        yield return new ConfigFieldDescriptor("Token", "API Token", ConfigFieldType.Password, true);
        yield return new ConfigFieldDescriptor("Port", "Port", ConfigFieldType.Number, false, "8080");
        
        // Expert mode field (hidden by default)
        yield return new ConfigFieldDescriptor("WebhookId", "Webhook ID", ConfigFieldType.Text) { IsExpertOnly = true };
    }
}
```

- **Key**: The internal name used to store the setting in the dictionary.
- **Label**: The string displayed in the UI.
- **Type**: `Text`, `Password`, `Number`, or `Boolean` (renders as a `CheckBox`).
- **IsRequired**: If true, the field should not be empty. The plugin provides automatic UI validation (red border) for required fields.
- **DefaultValue**: Optional fallback value.
- **IsExpertOnly**: If set to `true`, the field is hidden by default. The UI will automatically render an "Expert Mode" toggle switch for the user to reveal these advanced fields.

### 3. Lifecycle Methods

#### Initialize
Called when the backend is instantiated. Use `config.GetSetting(key)` to retrieve the values defined in `ConfigFields`.

```csharp
public void Initialize(SmartSwitchConfig config) {
    string host = config.GetSetting("Host");
    string token = config.GetSetting("Token");
    // Setup your internal HTTP client or connection here
}
```

#### State and Control
- `GetStateAsync()`: Should return `true` if the switch is ON.
- `TurnOnAsync()`: Power on the device.
- `TurnOffAsync()`: Power off the device.
- `SetStateAsync(bool targetState, int delaySeconds)`: 
    - If `delaySeconds == 0`, simply call `TurnOn` or `TurnOff`.
    - If `delaySeconds > 0` and the hardware supports it, trigger a hardware timer.
- `SupportsHardwareTimer`: Return `true` if the hardware supports an automatic "toggle back after X seconds" feature.
- `Dispose()`: Clean up resources like `HttpClient` instances or timers.

### 4. Networking and Timeouts

To respect the global timeout setting configured by the user, **always** use the `SmartSwitchHttpClient.GetCts()` helper when making network requests:

```csharp
using var cts = SmartSwitchHttpClient.GetCts();
var response = await httpClient.GetAsync(url, cts.Token);
```

### 5. Implementation Rules for AI Agents

- **Clean Break**: Only use the dynamic `GetSetting` methods. Do not add hardcoded properties to `SmartSwitchConfig`.
- **Statelessness**: The backend instances are created when needed. Store configuration state, but do not rely on long-lived connections unless handled by a singleton.
- **Error Handling**: Log errors using `NINA.Core.Utility.Logger`. Throw descriptive exceptions during `Initialize` if the configuration is invalid.
- **MEF Compatibility**: Do not use custom objects in the `ExportBackend` attribute arguments. Only use strings, bools, and ints. Use the `ConfigFields` property for complex metadata.

## Starting with a Template

For a faster start, you can copy the fully commented template files from the repository:

- **Backend Logic**: [TemplateBackend.cs](file:///c:/Users/Dirk/Documents/Programming/NINA.Plugin.SmartSwitchManager/Providers/Template/TemplateBackend.cs)
- **Network Scanner**: [TemplateScanner.cs](file:///c:/Users/Dirk/Documents/Programming/NINA.Plugin.SmartSwitchManager/Providers/Template/TemplateScanner.cs)

These files contain detailed explanations for every method and demonstrate how to use all available configuration field types.

## Example Implementation Sketch

```csharp
using NINA.Plugin.SmartSwitchManager.Core;
using NINA.Plugin.SmartSwitchManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyNamespace {
    [ExportBackend("GenericRest", "Generic REST Switch")]
    public class GenericRestBackend : ISmartSwitchBackend {
        private string url;

        // Dynamic Configuration Fields
        public IEnumerable<ConfigFieldDescriptor> ConfigFields {
            get {
                yield return new ConfigFieldDescriptor("Url", "Endpoint URL", ConfigFieldType.Text, true);
            }
        }

        public bool SupportsHardwareTimer => false;

        public void Initialize(SmartSwitchConfig config) {
            this.url = config.GetSetting("Url");
        }

        public async Task<bool> GetStateAsync() {
            // Implementation
            return true; 
        }

        public async Task TurnOnAsync() => await SetStateAsync(true);
        public async Task TurnOffAsync() => await SetStateAsync(false);

        public async Task SetStateAsync(bool targetState, int delaySeconds = 0) {
            // Implementation
        }
    }
}
```
