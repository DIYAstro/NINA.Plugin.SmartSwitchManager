using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] Unique identifier of this plugin — do NOT change after first release!
[assembly: Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]

// [MANDATORY] Assembly versioning — increment for each new release
[assembly: AssemblyVersion("0.9.0.0")]
[assembly: AssemblyFileVersion("0.9.0.0")]

// [MANDATORY] The name of the plugin (also used as folder name and DataTemplate key)
[assembly: AssemblyTitle("Smart Switch Manager")]
// [MANDATORY] Short description of the plugin
[assembly: AssemblyDescription("Manage smart home switches (Shelly, Tasmota, HA, ESPHome) as native N.I.N.A. equipment.")]

// Author
[assembly: AssemblyCompany("Dirk Morlak")]
// Product name
[assembly: AssemblyProduct("Smart Switch Manager")]
[assembly: AssemblyCopyright("Copyright © 2026 Dirk Morlak")]

// Minimum N.I.N.A. version this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]

// License
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// Repository
[assembly: AssemblyMetadata("Repository", "https://github.com/DIYAstro/NINA.Plugin.SmartSwitchManager")]

// Homepage
[assembly: AssemblyMetadata("Homepage", "https://github.com/DIYAstro/NINA.Plugin.SmartSwitchManager")]

// Tags for plugin search
[assembly: AssemblyMetadata("Tags", "Switch,Shelly,SmartHome,Relay,IoT")]

// Changelog
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/DIYAstro/NINA.Plugin.SmartSwitchManager/blob/main/CHANGELOG.md")]

// Optional metadata
[assembly: AssemblyMetadata("FeaturedImageURL", "pack://application:,,,/NINA.Plugin.SmartSwitchManager;component/plugin_logo.png")]
[assembly: AssemblyMetadata("Logo", "plugin_logo.png")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
[assembly: AssemblyMetadata("LongDescription", @"Smart Switch Manager integrates smart home switches into your N.I.N.A. workflow. Control power relays as native equipment for manual use or automation in the Advanced Sequencer.

Supported Providers:
• Shelly (Gen 1, Gen 2/3 Plus & Pro)
• Home Assistant (via LLAT REST API)
• Tasmota (via HTTP API)
• ESPHome (via REST API)")]

[assembly: ComVisible(false)]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
