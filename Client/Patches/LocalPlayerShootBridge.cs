using System;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 本地玩家开枪事件桥接器
    /// 订阅游戏内 ItemAgent_Gun.OnMainCharacterShootEvent 静态事件
    /// 并转发到 EventBus
    /// </summary>
    public class LocalPlayerShootBridge : IDisposable
    {
        private Type? _itemAgentGunType;
        private System.Reflection.PropertyInfo? _muzzleProperty;
        private Delegate? _shootEventHandler;
        private bool _initialized = false;

        /// <summary>
        /// 初始化桥接器
        /// </summary>
        public void Initialize()
        {
            try
            {
                if (_initialized)
                {
                    Debug.LogWarning("[LocalPlayerShootBridge] 已经初始化，跳过重复初始化");
                    return;
                }

                // 获取 ItemAgent_Gun 类型
                _itemAgentGunType = AccessTools.TypeByName("ItemAgent_Gun");
                if (_itemAgentGunType == null)
                {
                    Debug.LogError("[LocalPlayerShootBridge] 找不到 ItemAgent_Gun 类型");
                    return;
                }

                // 获取 muzzle 属性
                _muzzleProperty = AccessTools.Property(_itemAgentGunType, "muzzle");

                // 获取 OnMainCharacterShootEvent 静态事件
                var shootEvent = _itemAgentGunType.GetEvent("OnMainCharacterShootEvent");
                if (shootEvent == null)
                {
                    Debug.LogWarning("[LocalPlayerShootBridge] 找不到 OnMainCharacterShootEvent 事件");
                    return;
                }

                // 创建事件处理器并保存引用
                var handlerType = shootEvent.EventHandlerType;
                if (handlerType != null)
                {
                    var method = GetType().GetMethod(nameof(OnPlayerShoot), 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (method != null)
                    {
                        _shootEventHandler = Delegate.CreateDelegate(handlerType, this, method);
                        shootEvent.AddEventHandler(null, _shootEventHandler);
                        
                        _initialized = true;
                        Debug.Log("[LocalPlayerShootBridge] ✅ 已订阅 ItemAgent_Gun.OnMainCharacterShootEvent");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalPlayerShootBridge] 初始化失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 本地玩家开枪事件处理器
        /// </summary>
        private void OnPlayerShoot(object gun)
        {
            try
            {
                if (gun == null || !GameContext.IsInitialized) return;

                // 获取枪口位置和方向
                Transform? muzzle = _muzzleProperty?.GetValue(gun) as Transform;
                if (muzzle == null) return;

                Vector3 position = muzzle.position;
                Vector3 direction = muzzle.forward;
                // 发布到 EventBus
                var evt = new LocalPlayerShootEvent(gun, position, direction, muzzle);
                GameContext.Instance.EventBus.Publish(evt);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalPlayerShootBridge] 处理开枪事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (!_initialized || _itemAgentGunType == null || _shootEventHandler == null) return;

                // 取消订阅事件（使用保存的委托引用）
                var shootEvent = _itemAgentGunType.GetEvent("OnMainCharacterShootEvent");
                if (shootEvent != null)
                {
                    shootEvent.RemoveEventHandler(null, _shootEventHandler);
                }

                _shootEventHandler = null;
                _initialized = false;
                Debug.Log("[LocalPlayerShootBridge] 已取消订阅开枪事件");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalPlayerShootBridge] 清理失败: {ex.Message}");
            }
        }
    }
}

