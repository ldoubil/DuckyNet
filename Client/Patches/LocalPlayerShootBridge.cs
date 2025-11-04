using System;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 本地玩家开枪事件桥接器
    /// 通过 Harmony Patch 拦截 ShootOneBullet() 获取散射后的真实子弹方向
    /// </summary>
    public class LocalPlayerShootBridge : IDisposable
    {
        private Type? _itemAgentGunType;
        private System.Reflection.PropertyInfo? _muzzleProperty;
        private Delegate? _shootEventHandler;
        private bool _initialized = false;
        
        // 🔥 存储最后一次开火的散射方向（从 Harmony Patch 传递）
        private static Vector3 _lastScatteredDirection = Vector3.forward;
        private static Vector3 _lastMuzzlePosition = Vector3.zero;
        private static object? _lastGunInstance = null;

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

                // 🔥 优先使用从 Harmony Patch 捕获的散射后方向
                Vector3 position = _lastMuzzlePosition != Vector3.zero ? _lastMuzzlePosition : muzzle.position;
                Vector3 direction = (_lastGunInstance == gun && _lastScatteredDirection != Vector3.zero) 
                    ? _lastScatteredDirection 
                    : muzzle.forward;
                
                // 发布到 EventBus
                var evt = new LocalPlayerShootEvent(gun, position, direction, muzzle);
                GameContext.Instance.EventBus.Publish(evt);

                // 🔥 同步开火特效到服务器（使用散射后的方向）
                SendWeaponFireToServer(gun, position, direction);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalPlayerShootBridge] 处理开枪事件失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从 Harmony Patch 接收散射后的方向
        /// </summary>
        public static void OnBulletFired(object gunInstance, Vector3 muzzlePosition, Vector3 scatteredDirection)
        {
            _lastGunInstance = gunInstance;
            _lastMuzzlePosition = muzzlePosition;
            _lastScatteredDirection = scatteredDirection;
        }

        /// <summary>
        /// 发送开火数据到服务器
        /// </summary>
        private void SendWeaponFireToServer(object gun, Vector3 position, Vector3 direction)
        {
            try
            {
                if (!GameContext.IsInitialized || GameContext.Instance?.RpcClient == null)
                {
                    return; // RPC 未初始化，跳过
                }

                // 获取是否使用消音器
                bool isSilenced = false;
                if (_itemAgentGunType != null)
                {
                    var silencedProperty = AccessTools.Property(_itemAgentGunType, "Silenced");
                    if (silencedProperty != null)
                    {
                        isSilenced = (bool)(silencedProperty.GetValue(gun) ?? false);
                    }
                }

                // 创建开火数据
                var fireData = new Shared.Data.WeaponFireData
                {
                    MuzzlePositionX = position.x,
                    MuzzlePositionY = position.y,
                    MuzzlePositionZ = position.z,
                    MuzzleDirectionX = direction.x,
                    MuzzleDirectionY = direction.y,
                    MuzzleDirectionZ = direction.z,
                    IsSilenced = isSilenced,
                    WeaponTypeId = 0
                };

                // 创建服务代理
                var clientContext = new RPC.ClientServerContext(GameContext.Instance.RpcClient);
                var weaponService = new Shared.Services.Generated.WeaponSyncServiceClientProxy(clientContext);

                // 发送到服务器（单向通知）
                weaponService.NotifyWeaponFire(fireData);

                Debug.Log($"[LocalPlayerShootBridge] ✅ 开火数据已发送到服务器");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalPlayerShootBridge] 发送失败: {ex.Message}");
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

    /// <summary>
    /// Harmony Patch: 拦截 ItemAgent_Gun.ShootOneBullet() 获取散射后的真实方向
    /// </summary>
    [HarmonyPatch]
    public static class ShootOneBulletPatch
    {
        /// <summary>
        /// 目标方法：ItemAgent_Gun.ShootOneBullet
        /// </summary>
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("ItemAgent_Gun");
            return AccessTools.Method(type, "ShootOneBullet");
        }

        /// <summary>
        /// 后置补丁：捕获散射后的方向
        /// </summary>
        static void Postfix(
            object __instance,
            Vector3 _muzzlePoint,
            Vector3 _shootDirection,  // 🔥 这是散射后的真实方向！
            Vector3 firstFrameCheckStartPoint)
        {
            try
            {
                // 只处理主角色的开枪
                var holderProperty = AccessTools.Property(__instance.GetType(), "Holder");
                if (holderProperty != null)
                {
                    object? holder = holderProperty.GetValue(__instance);
                    if (holder != null)
                    {
                        var isMainCharacterProperty = AccessTools.Property(holder.GetType(), "IsMainCharacter");
                        bool isMainCharacter = (bool)(isMainCharacterProperty?.GetValue(holder) ?? false);
                        
                        if (isMainCharacter)
                        {
                            // 传递散射后的方向到桥接器
                            LocalPlayerShootBridge.OnBulletFired(__instance, _muzzlePoint, _shootDirection);
                            
                            #if DEBUG || UNITY_EDITOR
                            Debug.Log($"[ShootOneBulletPatch] 捕获散射方向: {_shootDirection}");
                            Debug.Log($"    • 枪口位置: {_muzzlePoint}");
                            #endif
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShootOneBulletPatch] 处理失败: {ex.Message}");
            }
        }
    }
}

