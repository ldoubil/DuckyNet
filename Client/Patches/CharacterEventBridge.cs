using System;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 单位事件桥接器 - 订阅游戏内现有的静态事件
    /// 负责将游戏内事件转发到 EventBus
    /// </summary>
    public class CharacterEventBridge : IDisposable
    {
        private static Type? _healthType;
        private static System.Reflection.MethodInfo? _tryGetCharacterMethod;
        private static bool _initialized = false;

        /// <summary>
        /// 初始化桥接器
        /// </summary>
        public void Initialize()
        {
            try
            {
                if (_initialized)
                {
                    Debug.LogWarning("[CharacterEventBridge] 已经初始化，跳过重复初始化");
                    return;
                }

                // 获取 Health 类型
                _healthType = AccessTools.TypeByName("Health");
                if (_healthType == null)
                {
                    Debug.LogError("[CharacterEventBridge] 找不到 Health 类型");
                    return;
                }

                // 获取 TryGetCharacter 方法
                _tryGetCharacterMethod = AccessTools.Method(_healthType, "TryGetCharacter");

                // 订阅 Health.OnDead 静态事件
                var onDeadEvent = _healthType.GetEvent("OnDead");
                if (onDeadEvent != null)
                {
                    // 创建委托
                    var handlerType = onDeadEvent.EventHandlerType;
                    var handler = Delegate.CreateDelegate(handlerType, this, nameof(OnCharacterDead));
                    
                    // 订阅事件
                    onDeadEvent.AddEventHandler(null, handler);
                    
                    _initialized = true;
                    Debug.Log("[CharacterEventBridge] ✅ 已订阅 Health.OnDead 事件");
                }
                else
                {
                    Debug.LogWarning("[CharacterEventBridge] 找不到 Health.OnDead 事件");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterEventBridge] 初始化失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 单位死亡事件处理器
        /// </summary>
        private void OnCharacterDead(object health, object damageInfo)
        {
            try
            {
                if (health == null || !GameContext.IsInitialized) return;

                // 尝试获取 CharacterMainControl
                object? characterMainControl = null;
                GameObject? gameObject = null;

                if (_tryGetCharacterMethod != null)
                {
                    characterMainControl = _tryGetCharacterMethod.Invoke(health, null);
                    
                    if (characterMainControl is Component component)
                    {
                        gameObject = component.gameObject;
                    }
                }

                // 发布死亡事件到 EventBus
                var evt = new CharacterDeathEvent(health, damageInfo, characterMainControl, gameObject);
                GameContext.Instance.EventBus.Publish(evt);

                #if DEBUG || UNITY_EDITOR
                Debug.Log($"[CharacterEventBridge] 单位死亡: Name={gameObject?.name ?? "Unknown"}");
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterEventBridge] 处理死亡事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (!_initialized || _healthType == null) return;

                // 取消订阅 Health.OnDead
                var onDeadEvent = _healthType.GetEvent("OnDead");
                if (onDeadEvent != null)
                {
                    var handlerType = onDeadEvent.EventHandlerType;
                    var handler = Delegate.CreateDelegate(handlerType, this, nameof(OnCharacterDead));
                    onDeadEvent.RemoveEventHandler(null, handler);
                }

                _initialized = false;
                Debug.Log("[CharacterEventBridge] 已取消订阅单位事件");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterEventBridge] 清理失败: {ex.Message}");
            }
        }
    }
}

