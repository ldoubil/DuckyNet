using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using ItemStatsSystem;
using Duckov.Utilities;
using UnityEngine;
using System;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 武器同步客户端服务实现
    /// 接收来自服务器的武器同步通知
    /// </summary>
    public class WeaponSyncClientServiceImpl : IWeaponSyncClientService
    {
        /// <summary>
        /// 接收其他玩家的武器槽位更新通知
        /// </summary>
        public void OnWeaponSlotUpdated(WeaponSlotUpdateNotification notification)
        {
            try
            {
                if (!GameContext.IsInitialized)
                {
                    Debug.LogWarning("[WeaponSyncClientService] GameContext 未初始化");
                    return;
                }

                // 过滤自己的更新
                var localPlayerId = GameContext.Instance.PlayerManager?.LocalPlayer?.Info.SteamId;
                if (!string.IsNullOrEmpty(localPlayerId) && localPlayerId == notification.PlayerId)
                {
                    Debug.Log("[WeaponSyncClientService] 跳过本地玩家的武器更新");
                    return;
                }

                string action = notification.ItemTypeId > 0 ? "装备" : "卸下";
                Debug.Log($"[WeaponSyncClientService] 收到武器更新: 玩家={notification.PlayerId}, 槽位={notification.SlotType}, 动作={action}, 武器={notification.ItemName}");

                // 获取远程玩家
                var remotePlayer = GameContext.Instance.PlayerManager?.GetRemotePlayer(notification.PlayerId);
                if (remotePlayer == null)
                {
                    Debug.LogWarning($"[WeaponSyncClientService] 找不到远程玩家: {notification.PlayerId}");
                    return;
                }

                // 创建武器数据
                WeaponItemData? weaponData = null;
                if (notification.ItemTypeId > 0)
                {
                    weaponData = new WeaponItemData
                    {
                        ItemTypeId = notification.ItemTypeId,
                        ItemName = notification.ItemName,
                        IsDefaultItem = notification.IsDefaultItem,
                        ItemDataCompressed = notification.ItemDataCompressed
                    };
                }

                // 更新远程玩家的武器数据
                remotePlayer.UpdateWeaponSlot(notification.SlotType, weaponData);

                // 如果角色已创建，立即应用武器
                if (remotePlayer.CharacterObject != null)
                {
                    ApplyWeaponToCharacter(remotePlayer, notification.SlotType, weaponData);
                }
                else
                {
                    Debug.Log($"[WeaponSyncClientService] 角色未创建，武器数据已保存，将在创建时应用");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponSyncClientService] 处理武器更新失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 接收所有玩家的武器数据（加入房间时）
        /// </summary>
        public void OnAllPlayersWeaponReceived(AllPlayersWeaponData allWeaponData)
        {
            try
            {
                if (!GameContext.IsInitialized)
                {
                    Debug.LogWarning("[WeaponSyncClientService] GameContext 未初始化");
                    return;
                }

                Debug.Log($"[WeaponSyncClientService] 收到批量武器数据: {allWeaponData.PlayersWeapons.Count} 个玩家");

                var playerManager = GameContext.Instance.PlayerManager;
                if (playerManager == null)
                {
                    Debug.LogWarning("[WeaponSyncClientService] PlayerManager 未初始化");
                    return;
                }

                // 更新所有玩家的武器数据
                foreach (var kvp in allWeaponData.PlayersWeapons)
                {
                    string playerId = kvp.Key;
                    PlayerWeaponData weaponData = kvp.Value;

                    // 跳过自己
                    if (playerId == playerManager.LocalPlayer?.Info.SteamId)
                    {
                        Debug.Log($"[WeaponSyncClientService] 跳过本地玩家的武器数据");
                        continue;
                    }

                    // 获取远程玩家
                    var remotePlayer = playerManager.GetRemotePlayer(playerId);
                    if (remotePlayer == null)
                    {
                        Debug.LogWarning($"[WeaponSyncClientService] 找不到远程玩家: {playerId}，跳过武器数据");
                        continue;
                    }

                    // 设置武器数据
                    remotePlayer.SetWeaponData(weaponData);

                    Debug.Log($"[WeaponSyncClientService] 已更新玩家 {playerId} 的武器数据: {weaponData.GetEquippedCount()} 件武器");

                    // 如果角色已创建，立即应用所有武器
                    if (remotePlayer.CharacterObject != null)
                    {
                        ApplyAllWeaponsToCharacter(remotePlayer);
                    }
                }

                Debug.Log("[WeaponSyncClientService] ✅ 批量武器数据处理完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponSyncClientService] 处理批量武器数据失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 接收其他玩家的武器切换通知
        /// </summary>
        public void OnWeaponSwitched(WeaponSwitchNotification notification)
        {
            try
            {
                if (!GameContext.IsInitialized)
                {
                    Debug.LogWarning("[WeaponSyncClientService] GameContext 未初始化");
                    return;
                }

                // 过滤自己的切换
                var localPlayerId = GameContext.Instance.PlayerManager?.LocalPlayer?.Info.SteamId;
                if (!string.IsNullOrEmpty(localPlayerId) && localPlayerId == notification.PlayerId)
                {
                    Debug.Log("[WeaponSyncClientService] 跳过本地玩家的武器切换");
                    return;
                }

                Debug.Log($"[WeaponSyncClientService] 收到武器切换: 玩家={notification.PlayerId}, 槽位={notification.CurrentWeaponSlot}");

                // 获取远程玩家
                var remotePlayer = GameContext.Instance.PlayerManager?.GetRemotePlayer(notification.PlayerId);
                if (remotePlayer == null)
                {
                    Debug.LogWarning($"[WeaponSyncClientService] 找不到远程玩家: {notification.PlayerId}");
                    return;
                }

                // 更新远程玩家的当前武器槽位
                remotePlayer.SwitchWeaponSlot(notification.CurrentWeaponSlot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponSyncClientService] 处理武器切换失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 接收其他玩家的开枪特效通知
        /// </summary>
        public void OnWeaponFired(WeaponFireData fireData)
        {
            try
            {
                if (!GameContext.IsInitialized)
                {
                    return;
                }

                // 过滤自己的开枪
                var localPlayerId = GameContext.Instance.PlayerManager?.LocalPlayer?.Info.SteamId;
                if (!string.IsNullOrEmpty(localPlayerId) && localPlayerId == fireData.PlayerId)
                {
                    return;
                }

                // 🔍 调试日志：客户端接收到的数据
                Debug.Log($"[WeaponSyncClientService] 📥 收到开枪特效: 玩家={fireData.PlayerId}");
                Debug.Log($"    • 位置: ({fireData.MuzzlePositionX:F3}, {fireData.MuzzlePositionY:F3}, {fireData.MuzzlePositionZ:F3})");
                Debug.Log($"    • 方向: ({fireData.MuzzleDirectionX:F3}, {fireData.MuzzleDirectionY:F3}, {fireData.MuzzleDirectionZ:F3})");
                Debug.Log($"    • 消音: {fireData.IsSilenced}");

                // 获取远程玩家
                var remotePlayer = GameContext.Instance.PlayerManager?.GetRemotePlayer(fireData.PlayerId);
                if (remotePlayer == null || remotePlayer.CharacterObject == null)
                {
                    #if DEBUG || UNITY_EDITOR
                    Debug.LogWarning($"[WeaponSyncClientService] 找不到远程玩家或角色对象: {fireData.PlayerId}");
                    #endif
                    return;
                }

                // 播放开枪特效（使用 WeaponFireEffectsPlayer）
                WeaponFireEffectsPlayer.PlayFireEffects(remotePlayer.CharacterObject, fireData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponSyncClientService] 播放开枪特效失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用单个武器到角色
        /// </summary>
        private void ApplyWeaponToCharacter(Core.Players.RemotePlayer remotePlayer, WeaponSlotType slotType, WeaponItemData? weaponData)
        {
            try
            {
                Debug.Log($"[WeaponSyncClientService] 🔍 开始应用武器: 槽位={slotType}, 玩家={remotePlayer.Info.SteamName}");

                var characterMainControl = remotePlayer.CharacterObject?.GetComponent<CharacterMainControl>();
                if (characterMainControl == null || characterMainControl.CharacterItem == null)
                {
                    Debug.LogWarning($"[WeaponSyncClientService] 角色组件无效: CharacterObject={remotePlayer.CharacterObject != null}, CharacterMainControl={characterMainControl != null}");
                    return;
                }

                int slotHash = GetSlotHash(slotType);
                Debug.Log($"[WeaponSyncClientService] 槽位Hash: {slotHash} (字符串Hash={slotType.ToString().GetHashCode()})");
                
                var slot = characterMainControl.CharacterItem.Slots.GetSlot(slotHash);

                if (slot == null)
                {
                    Debug.LogWarning($"[WeaponSyncClientService] ❌ 槽位不存在: {slotType}, Hash={slotHash}");
                    Debug.LogWarning($"[WeaponSyncClientService] 可用槽位数量: {characterMainControl.CharacterItem.Slots.Count}");
                    
                    // 打印所有槽位信息
                    for (int i = 0; i < characterMainControl.CharacterItem.Slots.Count; i++)
                    {
                        var s = characterMainControl.CharacterItem.Slots[i];
                        Debug.Log($"[WeaponSyncClientService] 槽位[{i}]: Key={s.Key}, Hash={s.Key.GetHashCode()}, Content={s.Content?.DisplayName ?? "空"}");
                    }
                    return;
                }

                Debug.Log($"[WeaponSyncClientService] ✅ 找到槽位: {slot.Key}, 当前内容={slot.Content?.DisplayName ?? "空"}");

                if (weaponData != null && weaponData.ItemTypeId > 0)
                {
                    // 装备新武器 - 反序列化物品数据
                    Item? weaponItem = WeaponSyncHelper.DeserializeItem(
                        weaponData.ItemDataCompressed,
                        weaponData.ItemTypeId
                    );

                    if (weaponItem != null)
                    {
                        Debug.Log($"[WeaponSyncClientService] 反序列化武器成功: {weaponItem.DisplayName}, TypeID={weaponItem.TypeID}");
                        
                        bool success = slot.Plug(weaponItem, out Item unpluggedItem);
                        
                        Debug.Log($"[WeaponSyncClientService] Slot.Plug() 结果: {success}");
                        
                        if (success)
                        {
                            Debug.Log($"[WeaponSyncClientService] ✅ 已插入槽位: {slotType} = {weaponData.ItemName}");
                            Debug.Log($"[WeaponSyncClientService] 槽位当前内容: {slot.Content?.DisplayName ?? "空"}");

                            // 处理被替换的武器
                            if (unpluggedItem != null)
                            {
                                Debug.Log($"[WeaponSyncClientService] 销毁被替换的武器: {unpluggedItem.DisplayName}");
                                unpluggedItem.DestroyTree();
                            }

                            // 🔑 检查该槽位是否是当前手持槽位
                            var currentSlot = remotePlayer.GetWeaponData()?.CurrentWeaponSlot;
                            Debug.Log($"[WeaponSyncClientService] 当前手持槽位: {currentSlot?.ToString() ?? "未设置"}, 装备槽位: {slotType}");
                            
                            if (currentSlot.HasValue && currentSlot.Value == slotType)
                            {
                                Debug.Log($"[WeaponSyncClientService] 🔥 该槽位是当前手持槽位，立即显示武器");
                                try
                                {
                                    characterMainControl.ChangeHoldItem(weaponItem);
                                    Debug.Log($"[WeaponSyncClientService] ✅ 武器已显示（ItemAgent已创建）");
                                }
                                catch (Exception agentEx)
                                {
                                    Debug.LogError($"[WeaponSyncClientService] ChangeHoldItem 失败: {agentEx.Message}");
                                }
                            }
                            else
                            {
                                Debug.Log($"[WeaponSyncClientService] 武器已插入槽位（等待切换通知才显示）");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[WeaponSyncClientService] ❌ Slot.Plug() 失败: {slotType}");
                            weaponItem.DestroyTree();
                        }
                    }
                    else
                    {
                        Debug.LogError($"[WeaponSyncClientService] ❌ 武器反序列化失败");
                    }
                }
                else
                {
                    // 卸下武器
                    if (slot.Content != null)
                    {
                        var removed = slot.Unplug();
                        if (removed != null)
                        {
                            // 🔑 清除当前手持武器的 ItemAgent
                            try
                            {
                                if (characterMainControl.CurrentHoldItemAgent?.Item == removed)
                                {
                                    characterMainControl.ChangeHoldItem(null);
                                    Debug.Log($"[WeaponSyncClientService] ✅ 已清除武器显示");
                                }
                            }
                            catch (Exception agentEx)
                            {
                                Debug.LogError($"[WeaponSyncClientService] 清除武器显示失败: {agentEx.Message}");
                            }

                            removed.DestroyTree();
                            Debug.Log($"[WeaponSyncClientService] ✅ 已卸下武器: {slotType}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponSyncClientService] 应用武器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用所有武器到角色
        /// </summary>
        private void ApplyAllWeaponsToCharacter(Core.Players.RemotePlayer remotePlayer)
        {
            try
            {
                var weaponData = remotePlayer.GetWeaponData();
                if (weaponData == null)
                {
                    Debug.Log("[WeaponSyncClientService] 该玩家没有武器数据");
                    return;
                }

                Debug.Log($"[WeaponSyncClientService] 开始应用所有武器: {weaponData.GetEquippedCount()} 件");

                int successCount = 0;
                var weaponSlots = new[]
                {
                    (WeaponSlotType.PrimaryWeapon, weaponData.PrimaryWeapon),
                    (WeaponSlotType.SecondaryWeapon, weaponData.SecondaryWeapon),
                    (WeaponSlotType.MeleeWeapon, weaponData.MeleeWeapon)
                };

                foreach (var (slotType, weapon) in weaponSlots)
                {
                    if (weapon != null && weapon.ItemTypeId > 0)
                    {
                        ApplyWeaponToCharacter(remotePlayer, slotType, weapon);
                        successCount++;
                    }
                }

                Debug.Log($"[WeaponSyncClientService] ✅ 武器应用完成: {successCount}/{weaponData.GetEquippedCount()}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponSyncClientService] 应用所有武器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取槽位Hash值
        /// </summary>
        private int GetSlotHash(WeaponSlotType slotType)
        {
            return slotType switch
            {
                WeaponSlotType.PrimaryWeapon => "PrimaryWeapon".GetHashCode(),
                WeaponSlotType.SecondaryWeapon => "SecondaryWeapon".GetHashCode(),
                WeaponSlotType.MeleeWeapon => "MeleeWeapon".GetHashCode(),
                _ => 0
            };
        }
    }
}

