using System;
using HarmonyLib;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 装备槽位补丁 - 监控本地玩家的装备槽位进入/退出
    /// 拦截 CharacterEquipmentController 的装备变更方法
    /// 仅处理主角色（本地玩家）的装备变更，忽略其他玩家和NPC
    /// </summary>
    [HarmonyPatch]
    public static class EquipmentSlotPatch
    {
        // ==================== 护甲槽位 ====================
        
        /// <summary>
        /// 护甲槽位变更补丁
        /// </summary>
        [HarmonyPatch(typeof(CharacterEquipmentController), "ChangeArmorModel")]
        [HarmonyPostfix]
        private static void Postfix_ChangeArmorModel(CharacterEquipmentController __instance, Slot slot)
        {
            try
            {
                // 检查是否为本地玩家
                if (!IsMainCharacter(__instance))
                {
                    return;
                }

                if (slot?.Content != null)
                {
                    Debug.Log($"[装备补丁] 护甲已装备: {slot.Content.name}");
                }
                else
                {
                    Debug.Log("[装备补丁] 护甲已卸下");
                }

                // 发布事件到 EventBus
                PublishEquipmentEvent(new ArmorSlotChangedEvent(slot, slot?.Content, __instance));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[装备补丁] 护甲槽位处理失败: {ex.Message}");
            }
        }

        // ==================== 头盔槽位 ====================
        
        /// <summary>
        /// 头盔槽位变更补丁
        /// </summary>
        [HarmonyPatch(typeof(CharacterEquipmentController), "ChangeHelmatModel")]
        [HarmonyPostfix]
        private static void Postfix_ChangeHelmatModel(CharacterEquipmentController __instance, Slot slot)
        {
            try
            {
                // 检查是否为本地玩家
                if (!IsMainCharacter(__instance))
                {
                    return;
                }

                if (slot?.Content != null)
                {
                    Debug.Log($"[装备补丁] 头盔已装备: {slot.Content.name}");
                }
                else
                {
                    Debug.Log("[装备补丁] 头盔已卸下");
                }

                // 发布事件到 EventBus
                PublishEquipmentEvent(new HelmetSlotChangedEvent(slot, slot?.Content, __instance));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[装备补丁] 头盔槽位处理失败: {ex.Message}");
            }
        }

        // ==================== 面罩槽位 ====================
        
        /// <summary>
        /// 面罩槽位变更补丁
        /// </summary>
        [HarmonyPatch(typeof(CharacterEquipmentController), "ChangeFaceMaskModel")]
        [HarmonyPostfix]
        private static void Postfix_ChangeFaceMaskModel(CharacterEquipmentController __instance, Slot slot)
        {
            try
            {
                // 检查是否为本地玩家
                if (!IsMainCharacter(__instance))
                {
                    return;
                }

                if (slot?.Content != null)
                {
                    Debug.Log($"[装备补丁] 面罩已装备: {slot.Content.name}");
                }
                else
                {
                    Debug.Log("[装备补丁] 面罩已卸下");
                }

                // 发布事件到 EventBus
                PublishEquipmentEvent(new FaceMaskSlotChangedEvent(slot, slot?.Content, __instance));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[装备补丁] 面罩槽位处理失败: {ex.Message}");
            }
        }

        // ==================== 背包槽位 ====================
        
        /// <summary>
        /// 背包槽位变更补丁
        /// </summary>
        [HarmonyPatch(typeof(CharacterEquipmentController), "ChangeBackpackModel")]
        [HarmonyPostfix]
        private static void Postfix_ChangeBackpackModel(CharacterEquipmentController __instance, Slot slot)
        {
            try
            {
                // 检查是否为本地玩家
                if (!IsMainCharacter(__instance))
                {
                    return;
                }

                if (slot?.Content != null)
                {
                    Debug.Log($"[装备补丁] 背包已装备: {slot.Content.name}");
                }
                else
                {
                    Debug.Log("[装备补丁] 背包已卸下");
                }

                // 发布事件到 EventBus
                PublishEquipmentEvent(new BackpackSlotChangedEvent(slot, slot?.Content, __instance));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[装备补丁] 背包槽位处理失败: {ex.Message}");
            }
        }

        // ==================== 耳机槽位 ====================
        
        /// <summary>
        /// 耳机槽位变更补丁
        /// </summary>
        [HarmonyPatch(typeof(CharacterEquipmentController), "ChangeHeadsetModel")]
        [HarmonyPostfix]
        private static void Postfix_ChangeHeadsetModel(CharacterEquipmentController __instance, Slot slot)
        {
            try
            {
                // 检查是否为本地玩家
                if (!IsMainCharacter(__instance))
                {
                    return;
                }

                if (slot?.Content != null)
                {
                    Debug.Log($"[装备补丁] 耳机已装备: {slot.Content.name}");
                }
                else
                {
                    Debug.Log("[装备补丁] 耳机已卸下");
                }

                // 发布事件到 EventBus
                PublishEquipmentEvent(new HeadsetSlotChangedEvent(slot, slot?.Content, __instance));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[装备补丁] 耳机槽位处理失败: {ex.Message}");
            }
        }

        // ==================== 辅助方法 ====================
        
        /// <summary>
        /// 检查是否为本地玩家的装备控制器
        /// </summary>
        private static bool IsMainCharacter(CharacterEquipmentController equipmentController)
        {
            if (equipmentController == null)
            {
                return false;
            }

            // 方法 1: 通过 CharacterMainControl 组件检查
            var characterMainControl = equipmentController.GetComponent<CharacterMainControl>();
            if (characterMainControl != null)
            {
                return characterMainControl.IsMainCharacter;
            }

            // 方法 2: 通过 LevelManager 比对
            if (LevelManager.Instance != null && LevelManager.Instance.MainCharacter != null)
            {
                var mainEquipmentController = LevelManager.Instance.MainCharacter.EquipmentController;
                return equipmentController == mainEquipmentController;
            }

            return false;
        }

        /// <summary>
        /// 发布装备事件到 EventBus 并同步到服务器
        /// </summary>
        private static void PublishEquipmentEvent<T>(T equipmentEvent) where T : EquipmentSlotChangedEvent
        {
            // 多层空值检查，确保 EventBus 已正确初始化
            if (!GameContext.IsInitialized || 
                GameContext.Instance == null || 
                GameContext.Instance.EventBus == null)
            {
                #if DEBUG || UNITY_EDITOR
                Debug.LogWarning("[装备补丁] EventBus 未初始化，跳过事件发布");
                #endif
                return;
            }

            try
            {
                // 1. 发布本地事件到 EventBus
                GameContext.Instance.EventBus.Publish(equipmentEvent);
                
                // 2. 同步到服务器（异步，不等待）
                SendEquipmentUpdateToServerAsync(equipmentEvent).ConfigureAwait(false);
                
                #if DEBUG || UNITY_EDITOR
                LogEquipmentChange(equipmentEvent);
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[装备补丁] 事件发布失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 发送装备更新到服务器
        /// </summary>
        private static async System.Threading.Tasks.Task SendEquipmentUpdateToServerAsync<T>(T equipmentEvent) 
            where T : EquipmentSlotChangedEvent
        {
            try
            {
                var rpcClient = GameContext.Instance?.RpcClient;
                if (rpcClient == null)
                {
                    Debug.LogWarning("[装备补丁] RPC 客户端未初始化，跳过服务器同步");
                    return;
                }

                // 获取装备 TypeID
                int? itemTypeId = null;
                if (equipmentEvent.EquippedItem is ItemStatsSystem.Item item)
                {
                    itemTypeId = item.TypeID;
                }

                // 创建请求
                var request = new Shared.Data.EquipmentSlotUpdateRequest
                {
                    SlotType = equipmentEvent.SlotType,
                    ItemTypeId = itemTypeId
                };

                // 创建服务代理
                var clientContext = new RPC.ClientServerContext(rpcClient);
                var equipmentService = new Shared.Services.Generated.EquipmentServiceClientProxy(clientContext);

                // 发送到服务器
                bool success = await equipmentService.UpdateEquipmentSlotAsync(request);
                
                if (success)
                {
                    Debug.Log($"[装备补丁] ✅ 装备更新已同步到服务器: {equipmentEvent.SlotType}");
                }
                else
                {
                    Debug.LogWarning($"[装备补丁] ⚠️ 服务器拒绝装备更新");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[装备补丁] 同步到服务器失败: {ex.Message}");
            }
        }

        #if DEBUG || UNITY_EDITOR
        /// <summary>
        /// 记录装备变更日志（仅调试模式）
        /// </summary>
        private static void LogEquipmentChange<T>(T equipmentEvent) where T : EquipmentSlotChangedEvent
        {
            try
            {
                string action = equipmentEvent.IsEquipped ? "装备" : "卸下";
                string itemName = "无";
                
                // 安全的类型转换 - 检查是否为 Unity Object
                if (equipmentEvent.EquippedItem != null)
                {
                    if (equipmentEvent.EquippedItem is UnityEngine.Object unityObj)
                    {
                        // Unity Object 有 name 属性，可以安全访问
                        itemName = !string.IsNullOrEmpty(unityObj.name) 
                            ? unityObj.name 
                            : unityObj.GetType().Name;
                    }
                    else
                    {
                        // 非 Unity Object，使用类型名
                        itemName = equipmentEvent.EquippedItem.GetType().Name;
                    }
                }
                
                Debug.Log($"[装备补丁] 事件已发布: {equipmentEvent.SlotType} - {action} - {itemName}");
            }
            catch (Exception ex)
            {
                // 日志记录失败不应影响主逻辑
                Debug.LogWarning($"[装备补丁] 日志记录失败: {ex.Message}");
            }
        }
        #endif
    }
}

