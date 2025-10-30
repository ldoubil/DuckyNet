using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 场景监听器 - 通过 Harmony 反射监听游戏原生场景事件，并发布到 EventBus
    /// </summary>
    public class SceneListener : IDisposable
    {
        private Type? _levelManagerType;
        private bool _typesInitialized = false;
        private Delegate? _beginInitDelegate;
        private Delegate? _initDelegate;
        private object? _levelManager;

        /// <summary>
        /// 初始化并开始监听场景事件
        /// </summary>
        public void Initialize()
        {
            try
            {
                InitializeTypes();
                RegisterSceneCallbacks();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneListener] 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化游戏类型引用
        /// </summary>
        private void InitializeTypes()
        {
            try
            {
                _levelManagerType = AccessTools.TypeByName("LevelManager");
                _typesInitialized = _levelManagerType != null;

                if (_typesInitialized)
                {
                    UnityEngine.Debug.Log("[SceneListener] 游戏类型初始化成功");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[SceneListener] LevelManager类型未找到");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneListener] 类型初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册场景回调（通过LevelManager事件监听）
        /// </summary>
        private void RegisterSceneCallbacks()
        {
            if (!_typesInitialized || _levelManagerType == null)
            {
                UnityEngine.Debug.LogWarning("[SceneListener] 类型未初始化，无法注册回调");
                return;
            }

            try
            {
                // 获取 LevelManager 单例
                var instanceProp = AccessTools.Property(_levelManagerType, "Instance");
                _levelManager = instanceProp?.GetValue(null);
                if (_levelManager == null)
                {
                    UnityEngine.Debug.LogWarning("[SceneListener] LevelManager 实例未初始化");
                    return;
                }

                // 订阅关卡开始初始化事件（离开旧场景）
                var beginInitEvent = AccessTools.Field(_levelManagerType, "OnLevelBeginInitializing");
                if (beginInitEvent != null)
                {
                    _beginInitDelegate = Delegate.CreateDelegate(
                        beginInitEvent.FieldType,
                        this,
                        typeof(SceneListener).GetMethod(nameof(OnLevelBeginInitializing),
                            BindingFlags.NonPublic | BindingFlags.Instance)
                    );
                    var currentDelegate = beginInitEvent.GetValue(_levelManager) as Delegate;
                    var combined = Delegate.Combine(currentDelegate, _beginInitDelegate);
                    beginInitEvent.SetValue(_levelManager, combined);
                    UnityEngine.Debug.Log("[SceneListener] 已订阅 OnLevelBeginInitializing 事件");
                }

                // 订阅关卡初始化完成事件（进入新场景）
                var initEvent = AccessTools.Field(_levelManagerType, "OnLevelInitialized");
                if (initEvent != null)
                {
                    _initDelegate = Delegate.CreateDelegate(
                        initEvent.FieldType,
                        this,
                        typeof(SceneListener).GetMethod(nameof(OnLevelInitialized),
                            BindingFlags.NonPublic | BindingFlags.Instance)
                    );
                    var currentDelegate = initEvent.GetValue(_levelManager) as Delegate;
                    var combined = Delegate.Combine(currentDelegate, _initDelegate);
                    initEvent.SetValue(_levelManager, combined);
                    UnityEngine.Debug.Log("[SceneListener] 已订阅 OnLevelInitialized 事件");
                }

                UnityEngine.Debug.Log("[SceneListener] 场景回调注册成功");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneListener] 注册场景回调失败: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 关卡开始初始化事件处理（离开旧场景）
        /// </summary>
        private void OnLevelBeginInitializing()
        {
            try
            {
                UnityEngine.Debug.Log("[SceneListener] 关卡开始初始化");

                // 发布场景卸载事件到 EventBus
                // 注意：这里使用 GetCurrentLevelInfo 获取即将离开的场景名
                // 如果无法获取，SceneManager 会通过 CurrentScene 状态处理
                if (GameContext.IsInitialized)
                {
                    var eventBus = GameContext.Instance.EventBus;
                    var currentSceneName = GetCurrentLevelInfo();
                    if (!string.IsNullOrEmpty(currentSceneName))
                    {
                        eventBus.Publish(new SceneUnloadingEvent(currentSceneName));
                    }
                    else
                    {
                        eventBus.Publish(new SceneUnloadingEvent(""));
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneListener] OnLevelBeginInitializing 处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 关卡初始化完成事件处理（进入新场景）
        /// </summary>
        private void OnLevelInitialized()
        {
            try
            {
                UnityEngine.Debug.Log("[SceneListener] 关卡初始化完成事件被触发");

                // 延迟一小段时间再获取场景名（等待关卡信息完全准备好）
                _ = DelayedSceneNameCheck();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneListener] OnLevelInitialized 处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 延迟检查场景名称（给关卡一些时间完全初始化）
        /// </summary>
        private async System.Threading.Tasks.Task DelayedSceneNameCheck()
        {
            // 等待一小段时间让关卡完全初始化
            await System.Threading.Tasks.Task.Delay(500);

            // 尝试多次获取场景名（最多3次）
            for (int i = 0; i < 3; i++)
            {
                var sceneName = GetCurrentLevelInfo();
                if (!string.IsNullOrEmpty(sceneName))
                {
                    UnityEngine.Debug.Log($"[SceneListener] 关卡初始化完成，场景名: {sceneName} (尝试 {i + 1} 次)");

                    // 发布场景加载事件到 EventBus
                    if (GameContext.IsInitialized)
                    {
                        var eventBus = GameContext.Instance.EventBus;
                        eventBus.Publish(new SceneLoadedEvent(sceneName));
                    }
                    return;
                }

                UnityEngine.Debug.LogWarning($"[SceneListener] 第 {i + 1} 次获取场景名失败，等待后重试...");
                await System.Threading.Tasks.Task.Delay(200);
            }

            UnityEngine.Debug.LogError("[SceneListener] 无法获取场景名，关卡可能未完全初始化");
        }

        /// <summary>
        /// 获取当前关卡信息（只返回场景名称）
        /// </summary>
        public string? GetCurrentLevelInfo()
        {
            if (!_typesInitialized || _levelManagerType == null) return null;

            try
            {
                var instanceProp = AccessTools.Property(_levelManagerType, "Instance");
                object? levelManager = instanceProp?.GetValue(null);
                if (levelManager == null) return null;

                var getLevelInfoMethod = AccessTools.Method(_levelManagerType, "GetCurrentLevelInfo");
                if (getLevelInfoMethod == null) return null;

                object? levelInfo = getLevelInfoMethod.Invoke(levelManager, null);
                if (levelInfo == null) return null;

                var levelInfoType = levelInfo.GetType();
                var sceneNameField = AccessTools.Field(levelInfoType, "sceneName");

                // 安全地获取字符串字段
                string sceneName = string.Empty;
                if (sceneNameField != null)
                {
                    var sceneNameValue = sceneNameField.GetValue(levelInfo);
                    sceneName = sceneNameValue as string ?? sceneNameValue?.ToString() ?? string.Empty;
                }

                return string.IsNullOrEmpty(sceneName) ? null : sceneName;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneListener] 获取关卡信息失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取当前地图名称（别名方法）
        /// </summary>
        public string? GetCurrentMapName()
        {
            return GetCurrentLevelInfo();
        }

        /// <summary>
        /// 清理资源并取消事件订阅
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_levelManager != null && _levelManagerType != null)
                {
                    // 取消订阅关卡开始初始化事件
                    var beginInitEvent = AccessTools.Field(_levelManagerType, "OnLevelBeginInitializing");
                    if (beginInitEvent != null && _beginInitDelegate != null)
                    {
                        var currentDelegate = beginInitEvent.GetValue(_levelManager) as Delegate;
                        if (currentDelegate != null)
                        {
                            var removed = Delegate.Remove(currentDelegate, _beginInitDelegate);
                            beginInitEvent.SetValue(_levelManager, removed);
                        }
                    }

                    // 取消订阅关卡初始化完成事件
                    var initEvent = AccessTools.Field(_levelManagerType, "OnLevelInitialized");
                    if (initEvent != null && _initDelegate != null)
                    {
                        var currentDelegate = initEvent.GetValue(_levelManager) as Delegate;
                        if (currentDelegate != null)
                        {
                            var removed = Delegate.Remove(currentDelegate, _initDelegate);
                            initEvent.SetValue(_levelManager, removed);
                        }
                    }
                }

                _beginInitDelegate = null;
                _initDelegate = null;
                _levelManager = null;
                _levelManagerType = null;
                _typesInitialized = false;

                UnityEngine.Debug.Log("[SceneListener] 已清理并取消事件订阅");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneListener] 清理失败: {ex.Message}");
            }
        }
    }
}

