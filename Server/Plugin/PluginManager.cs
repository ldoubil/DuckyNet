using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DuckyNet.Server.Plugin
{
    /// <summary>
    /// 插件管理器
    /// 负责加载、管理和卸载插件
    /// </summary>
    public class PluginManager
    {
        private readonly List<IPlugin> _plugins = new List<IPlugin>();
        private readonly IPluginContext _context;
        private readonly object _lock = new object();

        public PluginManager(IPluginContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 从目录加载所有插件
        /// </summary>
        /// <param name="pluginDirectory">插件目录路径</param>
        public void LoadPluginsFromDirectory(string pluginDirectory)
        {
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
                    LoadPlugin(dllFile);
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
        public void LoadPlugin(string dllPath)
        {
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
                    
                    lock (_lock)
                    {
                        // 调用插件的加载方法
                        plugin.OnLoad(_context);
                        _plugins.Add(plugin);
                        
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
    }
}

