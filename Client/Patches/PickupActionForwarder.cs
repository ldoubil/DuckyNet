using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;
using DuckyNet.Client.Services;
using DuckyNet.Client.Core;
using System;
using System.Threading.Tasks;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 拾取动作转发器
    /// 拦截玩家拾取交互，将网络同步物品的拾取动作转发到服务器
    /// 采用 Prefix 模式在代理销毁前捕获网络标记信息
    /// </summary>
    [HarmonyPatch(typeof(InteractablePickup), "OnInteractStart")]
    public static class PickupActionForwarder
    {
        /// <summary>
        /// Prefix 钩子：在拾取交互开始前转发网络同步请求
        /// 必须在此阶段执行，因为交互完成后 ItemAgent 会被销毁
        /// </summary>
        [HarmonyPrefix]
        static void ForwardPickupAction(InteractablePickup __instance, CharacterMainControl character)
        {
            // 快速验证：检查基础前置条件
            if (!ShouldProcessPickup(character))
            {
                return;
            }

            // 提取物品代理和网络标记信息
            var pickupContext = ExtractPickupContext(__instance);
            if (!pickupContext.IsValid)
            {
                return;
            }

            // 验证是否为网络同步物品
            if (!pickupContext.HasNetworkTag)
            {
                // 本地物品，无需同步
                return;
            }

            Debug.Log($"[PickupActionForwarder] 检测到网络物品交互 → ID={pickupContext.NetworkId}, 名称={pickupContext.ItemName}");

            // 获取同步服务并转发请求
            var coordinator = GetItemNetworkCoordinator();
            if (coordinator != null)
            {
                _ = ForwardToServerAsync(coordinator, pickupContext.NetworkId, pickupContext.ItemName);
            }
        }

        /// <summary>
        /// 检查是否应该处理此拾取操作
        /// </summary>
        private static bool ShouldProcessPickup(CharacterMainControl character)
        {
            // 游戏上下文必须已初始化
            if (!GameContext.IsInitialized)
            {
                return false;
            }

            // 必须是本地玩家执行的操作
            if (CharacterMainControl.Main == null || character != CharacterMainControl.Main)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 拾取上下文信息
        /// </summary>
        private struct PickupContext
        {
            public bool IsValid;
            public uint NetworkId;
            public string ItemName;
            public bool HasNetworkTag;
        }

        /// <summary>
        /// 提取拾取操作的上下文信息
        /// </summary>
        private static PickupContext ExtractPickupContext(InteractablePickup interactable)
        {
            var context = new PickupContext { IsValid = false };

            if (interactable == null || interactable.ItemAgent == null)
            {
                return context;
            }

            var agent = interactable.ItemAgent;
            var item = agent.Item;

            if (item == null)
            {
                return context;
            }

            // 尝试获取网络标记
            var networkTag = agent.GetComponent<NetworkDropTag>();
            if (networkTag != null)
            {
                context.IsValid = true;
                context.NetworkId = networkTag.DropId;
                context.ItemName = item.DisplayName;
                context.HasNetworkTag = true;
            }
            else
            {
                // 本地物品（无网络标记）
                context.IsValid = true;
                context.ItemName = item.DisplayName;
                context.HasNetworkTag = false;
            }

            return context;
        }

        /// <summary>
        /// 获取物品网络协调器
        /// </summary>
        private static ItemNetworkCoordinator? GetItemNetworkCoordinator()
        {
            if (!GameContext.IsInitialized)
            {
                return null;
            }

            var coordinator = GameContext.Instance.ItemNetworkCoordinator;
            if (coordinator == null)
            {
                Debug.LogWarning("[PickupActionForwarder] 同步服务不可用");
                return null;
            }

            return coordinator;
        }

        /// <summary>
        /// 异步转发拾取请求到服务器
        /// </summary>
        private static async Task ForwardToServerAsync(ItemNetworkCoordinator coordinator, uint networkId, string itemName)
        {
            try
            {
                bool operationSuccess = await coordinator.PickupItemAsync(networkId);

                if (operationSuccess)
                {
                    Debug.Log($"[PickupActionForwarder] 转发成功 → ID={networkId}, 物品={itemName}");
                }
                else
                {
                    Debug.LogWarning($"[PickupActionForwarder] 转发失败 → ID={networkId}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PickupActionForwarder] 异步转发异常 → ID={networkId}\n" +
                              $"错误: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

