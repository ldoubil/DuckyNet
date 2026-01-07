using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DuckyNet.RPC.Core;
using DuckyNet.Server.Plugins.Core;
using DuckyNet.Server.Plugins.Modules;
using DuckyNet.Server.Plugins.Web;
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
        }

        private readonly List<IPlugin> _plugins = new List<IPlugin>();
        private readonly List<PluginEntryInfo> _configuredPlugins = new List<PluginEntryInfo>();
        private readonly PluginConfiguration _configuration;
        private readonly Dictionary<string, Type> _builtinPlugins;
        private readonly object _lock = new object();
        private IPluginContext? _context;

        public PluginManager(PluginConfiguration configuration)
        {
            _configuration = configuration;
            _builtinPlugins = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "CorePlugin", typeof(CorePlugin) },
                { "PlayerModule", typeof(PlayerModulePlugin) },
                { "RoomModule", typeof(RoomModulePlugin) },
                { "SceneModule", typeof(SceneModulePlugin) },
                { "NpcModule", typeof(NpcModulePlugin) },
                { "SyncModule", typeof(SyncModulePlugin) },
                { "WebPlugin", typeof(WebPlugin) }
            };
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

        public void LoadConfiguredPlugins(RpcServer server)
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
                pluginEntry.Plugin.ConfigureRpc(server);
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

            foreach (var dllFile in dllFiles)
            {
                try
                {
                    LoadPlugin(dllFile, server);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginManager] 加载插件失败: {dllFile}");
                    Console.WriteLine($"[PluginManager] 错误: {ex.Message}");
                }
            }

            Console.WriteLine($"[PluginManager] 已加载 {_plugins.Count} 个插件");
        }

        /// <summary>
        /// 从 DLL 文件加载插件
        /// </summary>
        /// <param name="dllPath">DLL 文件路径</param>
        public void LoadPlugin(string dllPath, RpcServer server)
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
                return;
            }

            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    // 创建插件实例
                    var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                    var entry = new PluginEntryInfo(plugin, PluginLayer.External);

                    lock (_lock)
                    {
                        // 调用插件的加载方法
                        plugin.OnLoad(_context!);
                        plugin.ConfigureRpc(server);
                        entry.Loaded = true;
                        _plugins.Add(plugin);
                        _configuredPlugins.Add(entry);
                        
                        Console.WriteLine($"[PluginManager] 已加载插件: {plugin.Name} v{plugin.Version} by {plugin.Author}");
                        Console.WriteLine($"[PluginManager]   描述: {plugin.Description}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginManager] 实例化插件失败: {pluginType.FullName}");
                    Console.WriteLine($"[PluginManager] 错误: {ex.Message}");
                }
            }
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
        }

        private void AddConfiguredPlugins(IEnumerable<PluginEntry> entries, PluginLayer layer)
        {
            foreach (var entry in entries.Where(e => e.Enabled))
            {
                if (!_builtinPlugins.TryGetValue(entry.Name, out var pluginType))
                {
                    Console.WriteLine($"[PluginManager] 未注册的插件: {entry.Name}");
                    continue;
                }

                var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                _configuredPlugins.Add(new PluginEntryInfo(plugin, layer));
            }
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
