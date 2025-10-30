using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DuckyNet.Client.RPC;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 调试模块管理器 - 自动发现和管理所有调试模块
    /// </summary>
    public class DebugModuleManager : IDisposable
    {
        private readonly RpcClient _client;
        private readonly List<IDebugModule> _modules;

        /// <summary>
        /// <summary>
        /// 获取所有模块
        /// </summary>
        public IReadOnlyList<IDebugModule> AllModules
        {
            get { return _modules.AsReadOnly(); }
        }

        public DebugModuleManager(RpcClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _modules = new List<IDebugModule>();
        }

        /// <summary>
        /// 发现并注册所有调试模块
        /// </summary>
        public void DiscoverAndRegisterModules()
        {
            try
            {
                // 使用反射自动发现所有实现了IDebugModule的类
                var assembly = Assembly.GetExecutingAssembly();
                var moduleTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && 
                               !t.IsAbstract && 
                               typeof(IDebugModule).IsAssignableFrom(t) &&
                               t != typeof(IDebugModule))
                    .ToList();

                foreach (var type in moduleTypes)
                {
                    try
                    {
                        // 尝试创建实例（尝试不同的构造函数签名）
                        IDebugModule? module = null;
                        
                        // 优先尝试带 RpcClient 参数的构造函数
                        var ctorWithClient = type.GetConstructor(new[] { typeof(RpcClient) });
                        if (ctorWithClient != null)
                        {
                            module = ctorWithClient.Invoke(new object[] { _client }) as IDebugModule;
                        }
                        else
                        {
                            // 尝试无参构造函数
                            var ctorNoParam = type.GetConstructor(Type.EmptyTypes);
                            if (ctorNoParam != null)
                            {
                                module = ctorNoParam.Invoke(null) as IDebugModule;
                            }
                        }

                        if (module != null)
                        {
                            _modules.Add(module);
                            UnityEngine.Debug.Log($"[DebugModuleManager] 已注册模块: {module.ModuleName} ({module.Category})");
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning($"[DebugModuleManager] 无法创建模块实例: {type.Name}（缺少合适的构造函数）");
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[DebugModuleManager] 创建模块 {type.Name} 失败: {ex.Message}");
                    }
                }

                UnityEngine.Debug.Log($"[DebugModuleManager] 模块发现完成，共找到 {_modules.Count} 个模块");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DebugModuleManager] 发现模块失败: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 手动注册模块（用于不支持反射发现的场景）
        /// </summary>
        public void RegisterModule(IDebugModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            if (_modules.Contains(module))
            {
                UnityEngine.Debug.LogWarning($"[DebugModuleManager] 模块 {module.ModuleName} 已存在，跳过注册");
                return;
            }

            _modules.Add(module);
            UnityEngine.Debug.Log($"[DebugModuleManager] 手动注册模块: {module.ModuleName} ({module.Category})");
        }

        /// <summary>
        /// 获取所有分类
        /// </summary>
        public List<string> GetAllCategories()
        {
            return _modules.Select(m => m.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        /// <summary>
        /// 根据分类获取模块
        /// </summary>
        public IEnumerable<IDebugModule> GetModulesByCategory(string category)
        {
            return _modules.Where(m => m.Category == category);
        }

        /// <summary>
        /// 更新所有模块
        /// </summary>
        public void Update()
        {
            foreach (var module in _modules.Where(m => m.IsEnabled))
            {
                try
                {
                    module.Update();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[DebugModuleManager] 模块 {module.ModuleName} 更新失败: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            foreach (var module in _modules)
            {
                try
                {
                    if (module is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[DebugModuleManager] 模块 {module.ModuleName} 清理失败: {ex.Message}");
                }
            }
            _modules.Clear();
        }
    }

    /// <summary>
    /// 调试模块接口
    /// </summary>
    public interface IDebugModule
    {
        string ModuleName { get; }
        string Category { get; }
        string Description { get; }
        bool IsEnabled { get; set; }
        void OnGUI();
        void Update();
    }
}

