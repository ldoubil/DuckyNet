using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DuckyNet.RPC.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DuckyNet.Server.Plugin
{
    /// <summary>
    /// 插件管理器
    /// 负责加载、管理和卸载插件
    /// </summary>
    public class PluginManager
    {
        private enum PluginLayer
        {
            Core,
            Module,
            Web,
            External
        }

        private class PluginEntryInfo
        {
            public PluginEntryInfo(IPlugin plugin, PluginLayer layer)
            {
                Plugin = plugin;
                Layer = layer;
            }

            public IPlugin Plugin { get; }
            public PluginLayer Layer { get; }
            public bool Loaded { get; set; }
            public bool RpcConfigured { get; set; }
        }

        private readonly List<IPlugin> _plugins = new List<IPlugin>();
        private readonly List<PluginEntryInfo> _configuredPlugins = new List<PluginEntryInfo>();
        private readonly PluginConfiguration _configuration;
        private readonly Dictionary<string, Type> _availablePlugins;
        private readonly object _lock = new object();
        private IPluginContext? _context;

        public PluginManager(PluginConfiguration configuration)
        {
            _configuration = configuration;
            _availablePlugins = DiscoverBuiltinPlugins();
        }

        public static PluginConfiguration LoadConfiguration(string configPath)
        {
            if (!File.Exists(configPath))
            {
                var defaultConfig = PluginConfiguration.CreateDefault();
                SaveConfiguration(configPath, defaultConfig);
                return defaultConfig;
            }

            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<PluginConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return config ?? PluginConfiguration.CreateDefault();
        }

        public static void SaveConfiguration(string configPath, PluginConfiguration configuration)
        {
            var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(configPath, json);
        }

        public void Initialize(IPluginContext context)
        {
            _context = context;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            EnsureConfiguredPlugins();
            foreach (var pluginEntry in _configuredPlugins
                         .Where(p => p.Layer == PluginLayer.Core || p.Layer == PluginLayer.Module))
            {
                pluginEntry.Plugin.ConfigureServices(services);
            }
        }

        public void ConfigureWebServices(IServiceCollection services)
        {
            EnsureConfiguredPlugins();
            foreach (var pluginEntry in _configuredPlugins.Where(p => p.Layer == PluginLayer.Web))
            {
                pluginEntry.Plugin.ConfigureServices(services);
            }
        }

        public void LoadConfiguredPlugins()
        {
            EnsureConfiguredPlugins();
            EnsureContext();

            foreach (var pluginEntry in _configuredPlugins)
            {
                if (pluginEntry.Loaded)
                {
                    continue;
                }

                pluginEntry.Plugin.OnLoad(_context!);
                pluginEntry.Loaded = true;
                _plugins.Add(pluginEntry.Plugin);

                Console.WriteLine($"[PluginManager] 已加载插件: {pluginEntry.Plugin.Name} v{pluginEntry.Plugin.Version} by {pluginEntry.Plugin.Author}");
                Console.WriteLine($"[PluginManager]   描述: {pluginEntry.Plugin.Description}");
            }
        }

        public void ConfigureWeb(IEndpointRouteBuilder endpoints)
        {
            EnsureConfiguredPlugins();
            foreach (var pluginEntry in _configuredPlugins.Where(p => p.Layer == PluginLayer.Web))
            {
                pluginEntry.Plugin.ConfigureWeb(endpoints);
            }
        }

        public void ConfigureRpc(RpcServer server)
        {
            EnsureConfiguredPlugins();
            EnsureContext();

            foreach (var pluginEntry in _configuredPlugins.Where(entry => entry.Loaded && !entry.RpcConfigured))
            {
                ConfigureRpcForEntry(server, pluginEntry);
            }
        }

        /// <summary>
        /// 从目录加载所有插件
        /// </summary>
        /// <param name="pluginDirectory">插件目录路径</param>
        public void LoadPluginsFromDirectory(string pluginDirectory, RpcServer server)
        {
            EnsureContext();

            if (!Directory.Exists(pluginDirectory))
            {
                Console.WriteLine($"[PluginManager] 插件目录不存在: {pluginDirectory}");
                Directory.CreateDirectory(pluginDirectory);
                Console.WriteLine($"[PluginManager] 已创建插件目录: {pluginDirectory}");
                return;
            }

            var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories);
            Console.WriteLine($"[PluginManager] 发现 {dllFiles.Length} 个 DLL 文件");

            var entriesToLoad = new List<PluginEntryInfo>();

            foreach (var dllFile in dllFiles)
            {
                try
                {
                    var entries = LoadPluginEntries(dllFile);
                    entriesToLoad.AddRange(entries);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginManager] 加载插件失败: {dllFile}");
                    Console.WriteLine($"[PluginManager] 错误: {ex.Message}");
                }
            }

            var sortedEntries = SortEntriesByDependencies(entriesToLoad, _plugins.Select(p => p.Name).ToList());
            foreach (var entry in sortedEntries)
            {
                LoadExternalPluginEntry(entry, server);
            }

            Console.WriteLine($"[PluginManager] 已加载 {_plugins.Count} 个插件");
        }

        /// <summary>
        /// 从 DLL 文件加载插件
        /// </summary>
        /// <param name="dllPath">DLL 文件路径</param>
        public void LoadPlugin(string dllPath, RpcServer server)
        {
            var entries = LoadPluginEntries(dllPath);
            if (entries.Count == 0)
            {
                return;
            }

            var sortedEntries = SortEntriesByDependencies(entries, _plugins.Select(p => p.Name).ToList());
            foreach (var entry in sortedEntries)
            {
                LoadExternalPluginEntry(entry, server);
            }
        }

        private List<PluginEntryInfo> LoadPluginEntries(string dllPath)
        {
            EnsureContext();

            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException($"插件文件不存在: {dllPath}");
            }

            // 加载程序集
            var assembly = Assembly.LoadFrom(dllPath);
            
            // 查找实现 IPlugin 接口的类型
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            if (pluginTypes.Count == 0)
            {
                Console.WriteLine($"[PluginManager] 未找到插件类: {dllPath}");
                return new List<PluginEntryInfo>();
            }

            var entries = new List<PluginEntryInfo>();
            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    // 创建插件实例
                    var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                    entries.Add(new PluginEntryInfo(plugin, PluginLayer.External));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginManager] 实例化插件失败: {pluginType.FullName}");
                    Console.WriteLine($"[PluginManager] 错误: {ex.Message}");
                }
            }

            return entries;
        }

        /// <summary>
        /// 卸载所有插件
        /// </summary>
        public void UnloadAllPlugins()
        {
            lock (_lock)
            {
                foreach (var plugin in _plugins)
                {
                    try
                    {
                        plugin.OnUnload();
                        Console.WriteLine($"[PluginManager] 已卸载插件: {plugin.Name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PluginManager] 卸载插件失败: {plugin.Name}");
                        Console.WriteLine($"[PluginManager] 错误: {ex.Message}");
                    }
                }
                _plugins.Clear();
            }
        }

        /// <summary>
        /// 更新所有插件（在主循环中调用）
        /// </summary>
        public void UpdatePlugins()
        {
            lock (_lock)
            {
                foreach (var plugin in _plugins)
                {
                    try
                    {
                        plugin.OnUpdate();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PluginManager] 插件更新异常: {plugin.Name}");
                        Console.WriteLine($"[PluginManager] 错误: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 获取已加载的插件列表
        /// </summary>
        public IReadOnlyList<IPlugin> GetLoadedPlugins()
        {
            lock (_lock)
            {
                return _plugins.AsReadOnly();
            }
        }

        /// <summary>
        /// 根据名称获取插件
        /// </summary>
        public IPlugin? GetPlugin(string name)
        {
            lock (_lock)
            {
                return _plugins.FirstOrDefault(p => p.Name == name);
            }
        }

        private void EnsureConfiguredPlugins()
        {
            if (_configuredPlugins.Count > 0)
            {
                return;
            }

            AddConfiguredPlugins(_configuration.CorePlugins, PluginLayer.Core);
            AddConfiguredPlugins(_configuration.ModulePlugins, PluginLayer.Module);
            AddConfiguredPlugins(_configuration.WebPlugins, PluginLayer.Web);
            var sortedEntries = SortEntriesByDependencies(_configuredPlugins, Array.Empty<string>());
            _configuredPlugins.Clear();
            _configuredPlugins.AddRange(sortedEntries);
        }

        private void AddConfiguredPlugins(IEnumerable<PluginEntry> entries, PluginLayer layer)
        {
            foreach (var entry in entries.Where(e => e.Enabled))
            {
                if (!_availablePlugins.TryGetValue(entry.Name, out var pluginType))
                {
                    Console.WriteLine($"[PluginManager] 未注册的插件: {entry.Name}");
                    continue;
                }

                var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                _configuredPlugins.Add(new PluginEntryInfo(plugin, layer));
            }
        }

        private Dictionary<string, Type> DiscoverBuiltinPlugins()
        {
            var discovered = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            var assembly = Assembly.GetExecutingAssembly();
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                    if (string.IsNullOrWhiteSpace(plugin.Name))
                    {
                        Console.WriteLine($"[PluginManager] 插件名称为空，跳过: {pluginType.FullName}");
                        continue;
                    }

                    if (discovered.ContainsKey(plugin.Name))
                    {
                        Console.WriteLine($"[PluginManager] 插件名称重复，跳过: {plugin.Name} ({pluginType.FullName})");
                        continue;
                    }

                    discovered.Add(plugin.Name, pluginType);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginManager] 插件发现失败: {pluginType.FullName}");
                    Console.WriteLine($"[PluginManager] 错误: {ex.Message}");
                }
            }

            return discovered;
        }

        private List<PluginEntryInfo> SortEntriesByDependencies(
            List<PluginEntryInfo> entries,
            IReadOnlyCollection<string> loadedPluginNames)
        {
            if (entries.Count <= 1)
            {
                return entries;
            }

            var entryByName = entries.ToDictionary(entry => entry.Plugin.Name, StringComparer.OrdinalIgnoreCase);
            var dependencyMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var originalIndex = entries
                .Select((entry, index) => new { entry.Plugin.Name, Index = index })
                .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var dependencies = new HashSet<string>(GetDependencies(entry.Plugin.GetType()), StringComparer.OrdinalIgnoreCase);
                dependencies.RemoveWhere(dep =>
                {
                    if (loadedPluginNames.Contains(dep))
                    {
                        return true;
                    }

                    if (!entryByName.ContainsKey(dep))
                    {
                        Console.WriteLine($"[PluginManager] 插件 {entry.Plugin.Name} 依赖未满足: {dep}");
                        return true;
                    }

                    return false;
                });

                dependencyMap[entry.Plugin.Name] = dependencies;
            }

            var remaining = new HashSet<string>(entryByName.Keys, StringComparer.OrdinalIgnoreCase);
            var sorted = new List<PluginEntryInfo>();

            while (remaining.Count > 0)
            {
                var ready = remaining
                    .Where(name => dependencyMap[name].Count == 0)
                    .Select(name => entryByName[name])
                    .OrderBy(entry => GetLayerOrder(entry.Layer))
                    .ThenBy(entry => originalIndex[entry.Plugin.Name])
                    .ToList();

                if (ready.Count == 0)
                {
                    Console.WriteLine("[PluginManager] 插件依赖存在循环，按原始顺序加载剩余插件。");
                    sorted.AddRange(remaining
                        .Select(name => entryByName[name])
                        .OrderBy(entry => originalIndex[entry.Plugin.Name]));
                    break;
                }

                foreach (var entry in ready)
                {
                    var name = entry.Plugin.Name;
                    sorted.Add(entry);
                    remaining.Remove(name);
                    foreach (var other in remaining)
                    {
                        dependencyMap[other].Remove(name);
                    }
                }
            }

            return sorted;
        }

        private static int GetLayerOrder(PluginLayer layer)
        {
            return layer switch
            {
                PluginLayer.Core => 0,
                PluginLayer.Module => 1,
                PluginLayer.Web => 2,
                PluginLayer.External => 3,
                _ => 99
            };
        }

        private static IEnumerable<string> GetDependencies(Type pluginType)
        {
            return pluginType
                .GetCustomAttributes(typeof(DependsOnAttribute), true)
                .OfType<DependsOnAttribute>()
                .SelectMany(attribute => attribute.Dependencies)
                .Where(dep => !string.IsNullOrWhiteSpace(dep))
                .Select(dep => dep.Trim());
        }

        private void LoadExternalPluginEntry(PluginEntryInfo entry, RpcServer server)
        {
            lock (_lock)
            {
                entry.Plugin.OnLoad(_context!);
                entry.Loaded = true;
                _plugins.Add(entry.Plugin);
                _configuredPlugins.Add(entry);
                ConfigureRpcForEntry(server, entry);

                Console.WriteLine($"[PluginManager] 已加载插件: {entry.Plugin.Name} v{entry.Plugin.Version} by {entry.Plugin.Author}");
                Console.WriteLine($"[PluginManager]   描述: {entry.Plugin.Description}");
            }
        }

        private void ConfigureRpcForEntry(RpcServer server, PluginEntryInfo entry)
        {
            if (entry.RpcConfigured)
            {
                return;
            }

            entry.Plugin.ConfigureRpc(server);
            entry.RpcConfigured = true;
        }

        private void EnsureContext()
        {
            if (_context == null)
            {
                throw new InvalidOperationException("PluginContext 尚未初始化");
            }
        }
    }
}
