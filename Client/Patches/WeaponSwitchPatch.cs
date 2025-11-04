using System;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Client.Core;
using ItemStatsSystem;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 武器切换补丁 - 监听本地玩家的武器切换操作
    /// 拦截 ItemAgentHolder.ChangeHoldItem 方法来同步当前手持的武器槽位
    /// 这是所有武器显示的最终路径，比 SwitchToWeapon 更可靠
    /// </summary>
    [HarmonyPatch]
    public static class WeaponSwitchPatch
    {
        /// <summary>
        /// 拦截 ChangeHoldItem - 所有武器显示的最终路径
        /// </summary>
        [HarmonyPatch(typeof(ItemAgentHolder), "ChangeHoldItem")]
        [HarmonyPostfix]
        private static void Postfix_ChangeHoldItem(ItemAgentHolder __instance, Item item)
        {
            try
            {
                // 检查是否为本地玩家
                if (!IsMainCharacter(__instance.characterController))
                    return;

                if (item == null)
                {
                    Debug.Log("[武器切换补丁] 武器已收起");
                    // 不需要同步收起操作，因为切换到空槽位也是一种"切换"
                    return;
                }

                // 获取武器所在的槽位索引
                int slotIndex = GetWeaponSlotIndex(__instance.characterController, item);
                if (slotIndex == -999)
                {
                    Debug.LogWarning($"[武器切换补丁] 无法确定武器槽位: {item.DisplayName}");
                    return;
                }

                string slotName = GetSlotName(slotIndex);
                Debug.Log($"[武器切换补丁] 显示武器: {item.DisplayName}, 槽位: {slotIndex} ({slotName})");

                // 发送切换事件到服务器
                SendWeaponSwitchToServerAsync(slotIndex).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[武器切换补丁] 处理失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 检查是否为本地玩家
        /// </summary>
        private static bool IsMainCharacter(CharacterMainControl characterMainControl)
        {
            if (characterMainControl == null)
                return false;

            return characterMainControl.IsMainCharacter;
        }

        /// <summary>
        /// 获取武器所在的槽位索引
        /// </summary>
        private static int GetWeaponSlotIndex(CharacterMainControl character, Item weapon)
        {
            if (character == null || weapon == null)
                return -999;

            // 检查三个武器槽位
            if (character.PrimWeaponSlot()?.Content == weapon)
                return 0;   // 主武器
            if (character.SecWeaponSlot()?.Content == weapon)
                return 1;   // 副武器
            if (character.MeleeWeaponSlot()?.Content == weapon)
                return -1;  // 近战武器

            return -999; // 未知槽位
        }

        /// <summary>
        /// 获取槽位名称
        /// </summary>
        private static string GetSlotName(int index)
        {
            return index switch
            {
                -1 => "近战武器",
                0 => "主武器",
                1 => "副武器",
                _ => "未知"
            };
        }

        /// <summary>
        /// 将武器切换索引转换为 WeaponSlotType
        /// </summary>
        private static Shared.Data.WeaponSlotType? IndexToSlotType(int index)
        {
            return index switch
            {
                -1 => Shared.Data.WeaponSlotType.MeleeWeapon,
                0 => Shared.Data.WeaponSlotType.PrimaryWeapon,
                1 => Shared.Data.WeaponSlotType.SecondaryWeapon,
                _ => null
            };
        }

        /// <summary>
        /// 发送武器切换到服务器
        /// </summary>
        private static async System.Threading.Tasks.Task SendWeaponSwitchToServerAsync(int slotIndex)
        {
            try
            {
                if (!GameContext.IsInitialized || GameContext.Instance?.RpcClient == null)
                {
                    Debug.LogWarning("[武器切换补丁] RPC 客户端未初始化");
                    return;
                }

                var slotType = IndexToSlotType(slotIndex);
                if (!slotType.HasValue)
                {
                    Debug.LogWarning($"[武器切换补丁] 无效的槽位索引: {slotIndex}");
                    return;
                }

                // 创建请求
                var request = new Shared.Data.WeaponSwitchRequest
                {
                    CurrentWeaponSlot = slotType.Value
                };

                // 创建服务代理
                var clientContext = new RPC.ClientServerContext(GameContext.Instance.RpcClient);
                var weaponService = new Shared.Services.Generated.WeaponSyncServiceClientProxy(clientContext);

                // 发送到服务器
                bool success = await weaponService.SwitchWeaponSlotAsync(request);

                if (success)
                {
                    Debug.Log($"[武器切换补丁] ✅ 武器切换已同步到服务器: {slotType}");
                }
                else
                {
                    Debug.LogWarning($"[武器切换补丁] ⚠️ 服务器拒绝武器切换");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[武器切换补丁] 同步到服务器失败: {ex.Message}");
            }
        }
    }
}

