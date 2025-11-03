using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 物品同步客户端服务实现
    /// 接收来自服务器的物品同步通知
    /// </summary>
    public class ItemSyncClientServiceImpl : IItemSyncClientService
    {
        /// <summary>
        /// 接收远程玩家丢弃物品的通知
        /// </summary>
        public void OnRemoteItemDropped(ItemDropData dropData)
        {
            try
            {
                if (!GameContext.IsInitialized)
                {
                    Debug.LogWarning("[ItemSyncClientServiceImpl] GameContext 未初始化");
                    return;
                }

                var coordinator = GameContext.Instance.ItemNetworkCoordinator;
                if (coordinator == null)
                {
                    Debug.LogWarning("[ItemSyncClientServiceImpl] ItemNetworkCoordinator 未初始化");
                    return;
                }

                Debug.Log($"[ItemSyncClientServiceImpl] 收到远程物品丢弃通知 - DropId={dropData.DropId}, Item={dropData.ItemName}");

                // 转发到协调器处理
                coordinator.OnRemoteItemDropped(dropData);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ItemSyncClientServiceImpl] 处理远程物品丢弃失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 接收远程玩家拾取物品的通知
        /// </summary>
        public void OnRemoteItemPickedUp(uint dropId, string pickedByPlayerId)
        {
            try
            {
                if (!GameContext.IsInitialized)
                {
                    Debug.LogWarning("[ItemSyncClientServiceImpl] GameContext 未初始化");
                    return;
                }

                var coordinator = GameContext.Instance.ItemNetworkCoordinator;
                if (coordinator == null)
                {
                    Debug.LogWarning("[ItemSyncClientServiceImpl] ItemNetworkCoordinator 未初始化");
                    return;
                }

                Debug.Log($"[ItemSyncClientServiceImpl] 收到远程物品拾取通知 - DropId={dropId}, Player={pickedByPlayerId}");

                // 转发到协调器处理
                coordinator.OnRemoteItemPickedUp(dropId, pickedByPlayerId);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ItemSyncClientServiceImpl] 处理远程物品拾取失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

