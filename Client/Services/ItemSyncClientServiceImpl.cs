using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

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
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteItemDroppedEvent(dropData));
            }
        }

        /// <summary>
        /// 接收远程玩家拾取物品的通知
        /// </summary>
        public void OnRemoteItemPickedUp(uint dropId, string pickedByPlayerId)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteItemPickedUpEvent(dropId, pickedByPlayerId));
            }
        }
    }
}
