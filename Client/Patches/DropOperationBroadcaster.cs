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
    /// 物品丢弃操作广播器
    /// 当玩家丢弃物品时，将操作广播到网络中的其他玩家
    /// 采用 Postfix 模式确保游戏原生逻辑完成后再进行网络同步
    /// </summary>
    [HarmonyPatch(typeof(ItemExtensions), nameof(ItemExtensions.Drop), new Type[] { typeof(Item), typeof(Vector3), typeof(bool), typeof(Vector3), typeof(float) })]
    public static class DropOperationBroadcaster
    {
        /// <summary>
        /// Postfix 钩子：在物品成功丢弃后执行网络广播
        /// </summary>
        [HarmonyPostfix]
        static void BroadcastDropOperation(
            Item item,
            Vector3 pos,
            bool createRigidbody,
            Vector3 dropDirection,
            float randomAngle,
            DuckovItemAgent __result)
        {
            // 早期退出检查 - 快速验证前置条件
            if (!ValidateOperationContext(item, __result))
            {
                return;
            }

            // 获取同步服务
            var coordinator = GetItemNetworkCoordinator();
            if (coordinator == null)
            {
                return;
            }

            // 防止循环广播：检查是否为远程创建的物品
            if (coordinator.IsRemoteCreating(item))
            {
                Debug.Log($"[DropOperationBroadcaster] 忽略远程来源物品: {item.DisplayName}");
                return;
            }

            // 启动异步广播任务（非阻塞）
            _ = ExecuteBroadcastAsync(coordinator, item, pos, createRigidbody, dropDirection, randomAngle, __result);
        }

        /// <summary>
        /// 验证操作上下文的有效性
        /// </summary>
        private static bool ValidateOperationContext(Item item, DuckovItemAgent resultAgent)
        {
            // 游戏上下文必须已初始化
            if (!GameContext.IsInitialized)
            {
                return false;
            }

            // 必须存在本地主角色
            if (CharacterMainControl.Main == null)
            {
                return false;
            }

            // 物品和结果代理必须有效
            if (item == null || resultAgent == null)
            {
                Debug.LogWarning("[DropOperationBroadcaster] 无效的物品或代理对象");
                return false;
            }

            return true;
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
                Debug.LogWarning("[DropOperationBroadcaster] 物品同步服务未就绪");
                return null;
            }

            return coordinator;
        }

        /// <summary>
        /// 异步执行网络广播操作
        /// </summary>
        private static async Task ExecuteBroadcastAsync(
            ItemNetworkCoordinator coordinator,
            Item item,
            Vector3 position,
            bool withRigidbody,
            Vector3 direction,
            float angleVariation,
            DuckovItemAgent agentReference)
        {
            try
            {
                // 向服务器提交丢弃请求并获取全局唯一标识符
                var globalDropIdentifier = await coordinator.DropItemAsync(
                    item, position, withRigidbody, direction, angleVariation);

                // 处理服务器响应
                if (globalDropIdentifier.HasValue && globalDropIdentifier.Value > 0)
                {
                    // 在本地注册该物品与服务器ID的映射关系（网络同步物品）
                    coordinator.RegisterLocalDrop(globalDropIdentifier.Value, agentReference);
                    Debug.Log($"[DropOperationBroadcaster] 网络同步完成 → ID={globalDropIdentifier}, 物品={item.DisplayName}");
                }
                else
                {
                    // 不在房间中，物品仅本地可见（正常情况）
                    Debug.Log($"[DropOperationBroadcaster] 本地丢弃（仅自己可见） - 物品={item.DisplayName}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DropOperationBroadcaster] 广播失败 → 物品={item?.DisplayName ?? "Unknown"}\n" +
                              $"错误: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

