using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;
using ItemStatsSystem;
using Duckov.Utilities;
using UnityEngine;
using System;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 装备同步客户端服务实现
    /// 接收来自服务器的装备同步通知
    /// </summary>
    public class EquipmentClientServiceImpl : IEquipmentClientService
    {
        /// <summary>
        /// 接收其他玩家的装备槽位更新通知
        /// </summary>
        public void OnEquipmentSlotUpdated(EquipmentSlotUpdateNotification notification)
        {
            try
            {
                if (!GameContext.IsInitialized)
                {
                    Debug.LogWarning("[EquipmentClientService] GameContext 未初始化");
                    return;
                }

                // 过滤自己的更新（理论上服务器不会发送，但做双重检查）
                var localPlayerId = GameContext.Instance.PlayerManager?.LocalPlayer?.Info.SteamId;
                if (!string.IsNullOrEmpty(localPlayerId) && localPlayerId == notification.PlayerId)
                {
                    Debug.Log("[EquipmentClientService] 跳过本地玩家的装备更新");
                    return;
                }

                string action = notification.ItemTypeId.HasValue && notification.ItemTypeId.Value > 0 ? "装备" : "卸下";
                Debug.Log($"[EquipmentClientService] 收到装备更新: 玩家={notification.PlayerId}, 槽位={notification.SlotType}, 动作={action}, TypeID={notification.ItemTypeId}");

                // 获取远程玩家
                var remotePlayer = GameContext.Instance.PlayerManager?.GetRemotePlayer(notification.PlayerId);
                if (remotePlayer == null)
                {
                    Debug.LogWarning($"[EquipmentClientService] 找不到远程玩家: {notification.PlayerId}");
                    return;
                }

                // 更新远程玩家的装备数据
                remotePlayer.UpdateEquipmentSlot(notification.SlotType, notification.ItemTypeId);

                // 如果角色已创建，立即应用装备
                if (remotePlayer.CharacterObject != null)
                {
                    ApplyEquipmentToCharacter(remotePlayer, notification.SlotType, notification.ItemTypeId);
                }
                else
                {
                    Debug.Log($"[EquipmentClientService] 角色未创建，装备数据已保存，将在创建时应用");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquipmentClientService] 处理装备更新失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 接收所有玩家的装备数据（加入房间时）
        /// </summary>
        public void OnAllPlayersEquipmentReceived(AllPlayersEquipmentData allEquipmentData)
        {
            try
            {
                if (!GameContext.IsInitialized)
                {
                    Debug.LogWarning("[EquipmentClientService] GameContext 未初始化");
                    return;
                }

                Debug.Log($"[EquipmentClientService] 收到批量装备数据: {allEquipmentData.PlayersEquipment.Count} 个玩家");

                var playerManager = GameContext.Instance.PlayerManager;
                if (playerManager == null)
                {
                    Debug.LogWarning("[EquipmentClientService] PlayerManager 未初始化");
                    return;
                }

                // 更新所有玩家的装备数据
                foreach (var kvp in allEquipmentData.PlayersEquipment)
                {
                    string playerId = kvp.Key;
                    PlayerEquipmentData equipmentData = kvp.Value;

                    // 跳过自己
                    if (playerId == playerManager.LocalPlayer?.Info.SteamId)
                    {
                        Debug.Log($"[EquipmentClientService] 跳过本地玩家的装备数据");
                        continue;
                    }

                    // 获取或创建远程玩家
                    var remotePlayer = playerManager.GetRemotePlayer(playerId);
                    if (remotePlayer == null)
                    {
                        Debug.LogWarning($"[EquipmentClientService] 找不到远程玩家: {playerId}，跳过装备数据");
                        continue;
                    }

                    // 更新装备数据
                    remotePlayer.SetEquipmentData(equipmentData);

                    Debug.Log($"[EquipmentClientService] 已更新玩家 {playerId} 的装备数据: {equipmentData.GetEquippedCount()} 件装备");

                    // 如果角色已创建，立即应用所有装备
                    if (remotePlayer.CharacterObject != null)
                    {
                        ApplyAllEquipmentToCharacter(remotePlayer);
                    }
                }

                Debug.Log("[EquipmentClientService] ✅ 批量装备数据处理完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquipmentClientService] 处理批量装备数据失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 应用单个装备到角色
        /// </summary>
        private void ApplyEquipmentToCharacter(Core.Players.RemotePlayer remotePlayer, EquipmentSlotType slotType, int? itemTypeId)
        {
            try
            {
                var characterMainControl = remotePlayer.CharacterObject?.GetComponent<CharacterMainControl>();
                if (characterMainControl == null || characterMainControl.CharacterItem == null)
                {
                    Debug.LogWarning($"[EquipmentClientService] 角色组件无效");
                    return;
                }

                int slotHash = GetSlotHash(slotType);
                var slot = characterMainControl.CharacterItem.Slots.GetSlot(slotHash);
                if (slot == null)
                {
                    Debug.LogWarning($"[EquipmentClientService] 槽位不存在: {slotType}");
                    return;
                }

                if (itemTypeId.HasValue && itemTypeId.Value > 0)
                {
                    // 装备新物品
                    bool success = Core.Utils.EquipmentTools.CreateAndEquip(
                        itemTypeId.Value,
                        slot,
                        unpluggedItem => unpluggedItem.DestroyTree()
                    );

                    if (success)
                    {
                        Debug.Log($"[EquipmentClientService] ✅ 已应用装备: {slotType} = TypeID {itemTypeId.Value}");
                    }
                    else
                    {
                        Debug.LogWarning($"[EquipmentClientService] 应用装备失败: {slotType}");
                    }
                }
                else
                {
                    // 卸下装备
                    if (slot.Content != null)
                    {
                        var removed = slot.Unplug();
                        if (removed != null)
                        {
                            removed.DestroyTree();
                            Debug.Log($"[EquipmentClientService] ✅ 已卸下装备: {slotType}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquipmentClientService] 应用装备失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用所有装备到角色
        /// </summary>
        private void ApplyAllEquipmentToCharacter(Core.Players.RemotePlayer remotePlayer)
        {
            try
            {
                var characterMainControl = remotePlayer.CharacterObject?.GetComponent<CharacterMainControl>();
                if (characterMainControl == null || characterMainControl.CharacterItem == null)
                {
                    Debug.LogWarning($"[EquipmentClientService] 角色组件无效");
                    return;
                }

                var equipmentData = remotePlayer.GetEquipmentData();
                if (equipmentData == null)
                {
                    Debug.Log("[EquipmentClientService] 该玩家没有装备数据");
                    return;
                }

                Debug.Log($"[EquipmentClientService] 开始应用所有装备: {equipmentData.GetEquippedCount()} 件");

                int successCount = 0;
                foreach (var kvp in equipmentData.Equipment)
                {
                    EquipmentSlotType slotType = kvp.Key;
                    int itemTypeId = kvp.Value;

                    if (itemTypeId > 0)
                    {
                        ApplyEquipmentToCharacter(remotePlayer, slotType, itemTypeId);
                        successCount++;
                    }
                }

                Debug.Log($"[EquipmentClientService] ✅ 装备应用完成: {successCount}/{equipmentData.GetEquippedCount()}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquipmentClientService] 应用所有装备失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取槽位Hash值
        /// </summary>
        private int GetSlotHash(EquipmentSlotType slotType)
        {
            return slotType switch
            {
                EquipmentSlotType.Armor => CharacterEquipmentController.armorHash,
                EquipmentSlotType.Helmet => CharacterEquipmentController.helmatHash,
                EquipmentSlotType.FaceMask => CharacterEquipmentController.faceMaskHash,
                EquipmentSlotType.Backpack => CharacterEquipmentController.backpackHash,
                EquipmentSlotType.Headset => CharacterEquipmentController.headsetHash,
                _ => 0
            };
        }
    }
}

