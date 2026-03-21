using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using NINA.Core.Utility;
using NINA.Plugin.SmartSwitchManager.Core.Models;

namespace NINA.Plugin.SmartSwitchManager.Core.Backends {

    /// <summary>
    /// Manages the discovery and loading of ISmartSwitchBackend implementations using MEF.
    /// </summary>
    public class BackendRegistry {
        private static BackendRegistry instance;
        public static BackendRegistry Instance => instance ??= new BackendRegistry();

        private static bool isAssemblyResolveHooked = false;
        private string pluginDirectory;

        private BackendRegistry() {
            discoveredBackends = Enumerable.Empty<Lazy<ISmartSwitchBackend, IBackendMetadata>>();
            discoveredScanners = Enumerable.Empty<Lazy<ISmartSwitchScanner, IDictionary<string, object>>>();
        }

        public void Initialize(string pluginDirectory) {
            this.pluginDirectory = pluginDirectory;
            if (!isAssemblyResolveHooked) {
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                isAssemblyResolveHooked = true;
            }

            try {
                var catalog = new AggregateCatalog();
                // We scan the assembly where the backends are defined. 
                // Since this is Core, we scan both Core and the plugin assembly if provided.
                catalog.Catalogs.Add(new AssemblyCatalog(typeof(BackendRegistry).Assembly));

                string backendsPath = Path.Combine(pluginDirectory, "Backends");
                if (Directory.Exists(backendsPath)) {
                    catalog.Catalogs.Add(new DirectoryCatalog(backendsPath));
                    // Scan subdirectories for provider-specific DLLs
                    foreach (var dir in Directory.EnumerateDirectories(backendsPath)) {
                        catalog.Catalogs.Add(new DirectoryCatalog(dir));
                    }
                }

                var container = new CompositionContainer(catalog);
                container.ComposeParts(this);

                Logger.Info($"BackendRegistry: Filtered discovery via MEF complete. Found {discoveredBackends?.Count() ?? 0} backends.");
            } catch (Exception ex) {
                Logger.Error($"BackendRegistry: Failed to initialize MEF discovery. {ex.Message}");
                discoveredBackends ??= Enumerable.Empty<Lazy<ISmartSwitchBackend, IBackendMetadata>>();
                discoveredScanners ??= Enumerable.Empty<Lazy<ISmartSwitchScanner, IDictionary<string, object>>>();
            }
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
            string assemblyName = new AssemblyName(args.Name).Name;
            
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
            if (loadedAssembly != null) return loadedAssembly;

            if (string.IsNullOrEmpty(pluginDirectory)) return null;
            string assemblyPath = Path.Combine(pluginDirectory, assemblyName + ".dll");
            
            if (File.Exists(assemblyPath)) {
                try {
                    return Assembly.LoadFrom(assemblyPath);
                } catch (Exception ex) {
                    Logger.Debug($"BackendRegistry: Failed to resolve assembly {assemblyName} from {pluginDirectory}: {ex.Message}");
                }
            }
            return null;
        }

        [ImportMany(typeof(ISmartSwitchBackend))]
        private IEnumerable<Lazy<ISmartSwitchBackend, IBackendMetadata>> discoveredBackends = null!;

        [ImportMany(typeof(ISmartSwitchScanner))]
        private IEnumerable<Lazy<ISmartSwitchScanner, IDictionary<string, object>>> discoveredScanners = null!;

        public IEnumerable<IBackendMetadata> AvailableProviders {
            get {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var b in discoveredBackends) {
                    if (seen.Add(b.Metadata.ProviderId)) {
                        yield return b.Metadata;
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new backend instance for the given provider ID directly via MEF.
        /// This avoids the double-instantiation that would occur if we used GetType() + Activator.CreateInstance.
        /// </summary>
        public ISmartSwitchBackend CreateBackendInstance(string providerId) {
            var matching = discoveredBackends
                .FirstOrDefault(b => b.Metadata.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase));
            
            if (matching == null) return null;

            // Use Activator.CreateInstance on the concrete type to get a FRESH instance,
            // without triggering MEF's own Lazy singleton (which would be shared/reused).
            Type concreteType = matching.Value.GetType();
            return (ISmartSwitchBackend)Activator.CreateInstance(concreteType);
        }

        public string GetResolvedProviderId(SmartSwitchConfig config) {
            if (config == null) return null;
            return config.ProviderType;
        }

        public IBackendMetadata GetMetadata(string providerId) {
            return discoveredBackends.FirstOrDefault(b => b.Metadata.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase))?.Metadata;
        }

        public bool SupportsHardwareTimer(string providerId) {
            var metadata = GetMetadata(providerId);
            return metadata?.SupportsHardwareTimer ?? false;
        }

        public ISmartSwitchScanner GetScanner(string providerId) {
            var scanner = discoveredScanners.FirstOrDefault(s => 
                s.Metadata.ContainsKey("ProviderId") && 
                s.Metadata["ProviderId"].ToString().Equals(providerId, StringComparison.OrdinalIgnoreCase));
            return scanner?.Value;
        }

        public IEnumerable<ConfigFieldDescriptor> GetConfigFields(string providerId) {
            var matching = discoveredBackends
                .FirstOrDefault(b => b.Metadata.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase));
            
            if (matching == null) return Enumerable.Empty<ConfigFieldDescriptor>();
            return matching.Value?.ConfigFields ?? Enumerable.Empty<ConfigFieldDescriptor>();
        }
    }

    internal class BackendMetadata : IBackendMetadata {
        public string ProviderId { get; }
        public string DisplayName { get; }
        public bool SupportsHardwareTimer { get; }
        public bool SupportsScanning { get; }

        public BackendMetadata(IDictionary<string, object> metadata) {
            ProviderId = metadata.ContainsKey(nameof(ProviderId)) ? metadata[nameof(ProviderId)].ToString() : "";
            DisplayName = metadata.ContainsKey(nameof(DisplayName)) ? metadata[nameof(DisplayName)].ToString() : ProviderId;
            SupportsHardwareTimer = metadata.ContainsKey(nameof(SupportsHardwareTimer)) && (bool)metadata[nameof(SupportsHardwareTimer)];
            SupportsScanning = metadata.ContainsKey(nameof(SupportsScanning)) && (bool)metadata[nameof(SupportsScanning)];
        }
    }
}
