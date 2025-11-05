using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 本地玩家开枪桥接器
    /// 🔥 方案一（队列批处理）：避免霰弹枪/连发武器的网络请求爆炸
    /// 
    /// 架构说明：
    /// 1. ShootOneBulletPatch 捕获每发子弹的散射数据 → 入队
    /// 2. OnMainCharacterShootEvent 触发时 → 批量处理队列
    /// 3. 霰弹枪 8 发弹丸 → 只需 1 次批量 RPC 调用 ✅
    /// </summary>
    public class LocalPlayerShootBridge : IDisposable
    {
        private Type? _itemAgentGunType;
        private System.Reflection.PropertyInfo? _muzzleProperty;
        private Delegate? _shootEventHandler;
        private bool _initialized = false;

        /// <summary>
        /// 子弹开火数据结构
        /// </summary>
        public struct BulletFireData
        {
            public Vector3 MuzzlePosition;
            public Vector3 ScatteredDirection;
        }

        // 🔥 使用队列存储多发子弹的散射数据
        private static Queue<BulletFireData> _pendingBullets = new Queue<BulletFireData>();
        private static object? _currentGunInstance = null;

        public void Initialize()
        {
            try
            {
                if (_initialized)
                {
                    Debug.LogWarning("[LocalPlayerShootBridge] 已经初始化,跳过重复初始化");
                    return;
                }

                _itemAgentGunType = AccessTools.TypeByName("ItemAgent_Gun");
                if (_itemAgentGunType == null)
                {
                    Debug.LogError("[LocalPlayerShootBridge] 找不到 ItemAgent_Gun 类型");
                    return;
                }

                _muzzleProperty = AccessTools.Property(_itemAgentGunType, "muzzle");

                var shootEvent = _itemAgentGunType.GetEvent("OnMainCharacterShootEvent");
                if (shootEvent == null)
                {
                    Debug.LogWarning("[LocalPlayerShootBridge] 找不到 OnMainCharacterShootEvent 事件");
                    return;
                }

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
                        Debug.Log("[LocalPlayerShootBridge] ✅ 已订阅 ItemAgent_Gun.OnMainCharacterShootEvent（队列批处理模式）");
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
        /// 开枪事件处理器 - 批量处理队列中的所有子弹
        /// 🎯 OnMainCharacterShootEvent 在所有 ShootOneBullet() 完成后触发
        /// </summary>
        private void OnPlayerShoot(object gun)
        {
            try
            {
                if (gun == null || !GameContext.IsInitialized) return;

                // 🔥 处理队列中的所有子弹数据
                if (_currentGunInstance == gun && _pendingBullets.Count > 0)
                {
                    Transform? muzzle = _muzzleProperty?.GetValue(gun) as Transform;

                    // 🎯 批量发送所有子弹（一次 RPC 调用）
                    SendBulletBatchToServer(gun, _pendingBullets);

                    // 🎯 逐个发布到 EventBus（供客户端其他系统使用）
                    while (_pendingBullets.Count > 0)
                    {
                        var bulletData = _pendingBullets.Dequeue();
                        var evt = new LocalPlayerShootEvent(gun, bulletData.MuzzlePosition, bulletData.ScatteredDirection, muzzle);
                        GameContext.Instance.EventBus.Publish(evt);
                    }

                    _currentGunInstance = null;
                    Debug.Log($"[LocalPlayerShootBridge] ✅ 已批量处理所有子弹");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalPlayerShootBridge] 处理开枪事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 批量发送子弹数据到服务器
        /// 🚀 性能优化：霰弹枪 8 发弹丸只需 1 次 RPC 调用
        /// </summary>
        private void SendBulletBatchToServer(object gun, Queue<BulletFireData> bullets)
        {
            try
            {
                if (!GameContext.IsInitialized || GameContext.Instance?.RpcClient == null)
                {
                    return;
                }

                if (bullets.Count == 0)
                {
                    return;
                }

                // 获取消音器状态
                bool isSilenced = false;
                if (_itemAgentGunType != null)
                {
                    var silencedProperty = AccessTools.Property(_itemAgentGunType, "Silenced");
                    if (silencedProperty != null)
                    {
                        isSilenced = (bool)(silencedProperty.GetValue(gun) ?? false);
                    }
                }

                int bulletCount = bullets.Count;

                // 🔥 创建批量数据结构（避免 RPC 数组序列化问题）
                var batchData = new WeaponFireBatchData
                {
                    BulletCount = bulletCount,
                    IsSilenced = isSilenced,
                    WeaponTypeId = 0,
                    MuzzlePositionsX = new float[bulletCount],
                    MuzzlePositionsY = new float[bulletCount],
                    MuzzlePositionsZ = new float[bulletCount],
                    DirectionsX = new float[bulletCount],
                    DirectionsY = new float[bulletCount],
                    DirectionsZ = new float[bulletCount]
                };

                // 填充批量数据
                int index = 0;
                foreach (var bulletData in bullets)
                {
                    batchData.MuzzlePositionsX[index] = bulletData.MuzzlePosition.x;
                    batchData.MuzzlePositionsY[index] = bulletData.MuzzlePosition.y;
                    batchData.MuzzlePositionsZ[index] = bulletData.MuzzlePosition.z;
                    batchData.DirectionsX[index] = bulletData.ScatteredDirection.x;
                    batchData.DirectionsY[index] = bulletData.ScatteredDirection.y;
                    batchData.DirectionsZ[index] = bulletData.ScatteredDirection.z;
                    index++;
                }

                // 🚀 批量发送（一次 RPC 调用）
                var clientContext = new RPC.ClientServerContext(GameContext.Instance.RpcClient);
                var weaponService = new Shared.Services.Generated.WeaponSyncServiceClientProxy(clientContext);
                weaponService.NotifyWeaponFireBatch(batchData);

                Debug.Log($"[LocalPlayerShootBridge] 🚀 批量发送完成: {bulletCount} 发子弹 (1 次 RPC 调用)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalPlayerShootBridge] 批量发送失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 Harmony Patch 接收散射后的方向（每发子弹调用一次）
        /// 🔥 霰弹枪/连发武器会多次调用，数据暂存到队列
        /// </summary>
        public static void OnBulletFired(object gunInstance, Vector3 muzzlePosition, Vector3 scatteredDirection)
        {
            _currentGunInstance = gunInstance;
            _pendingBullets.Enqueue(new BulletFireData
            {
                MuzzlePosition = muzzlePosition,
                ScatteredDirection = scatteredDirection
            });
        }

        public void Dispose()
        {
            try
            {
                if (!_initialized || _itemAgentGunType == null || _shootEventHandler == null) return;

                var shootEvent = _itemAgentGunType.GetEvent("OnMainCharacterShootEvent");
                if (shootEvent != null)
                {
                    shootEvent.RemoveEventHandler(null, _shootEventHandler);
                }

                _shootEventHandler = null;
                _initialized = false;
                _pendingBullets.Clear();
                Debug.Log("[LocalPlayerShootBridge] 已取消订阅开枪事件");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalPlayerShootBridge] 清理失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ShootOneBullet Patch - 捕获每发子弹的散射方向并入队
    /// 🎯 不立即发送，而是收集到队列中，等待 OnMainCharacterShootEvent 触发后批量处理
    /// </summary>
    [HarmonyPatch]
    public static class ShootOneBulletPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("ItemAgent_Gun");
            return AccessTools.Method(type, "ShootOneBullet");
        }

        /// <summary>
        /// Postfix - 在每发子弹发射后捕获散射数据
        /// </summary>
        /// <param name="__instance">ItemAgent_Gun 实例</param>
        /// <param name="_muzzlePoint">枪口位置</param>
        /// <param name="_shootDirection">散射后的射击方向</param>
        /// <param name="firstFrameCheckStartPoint">第一帧检测起点</param>
        static void Postfix(
            object __instance,
            Vector3 _muzzlePoint,
            Vector3 _shootDirection,
            Vector3 firstFrameCheckStartPoint)
        {
            try
            {
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
                            // 🔥 只收集数据，不发送（等待 OnMainCharacterShootEvent）
                            LocalPlayerShootBridge.OnBulletFired(__instance, _muzzlePoint, _shootDirection);
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
