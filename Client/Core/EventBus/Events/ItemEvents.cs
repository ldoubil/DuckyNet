using DuckyNet.Shared.Data;
using DuckyNet.Shared.Events;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 远程玩家丢弃物品事件
    /// </summary>
    public class RemoteItemDroppedEvent : EventBase
    {
        public ItemDropData DropData { get; }

        public RemoteItemDroppedEvent(ItemDropData dropData)
        {
            DropData = dropData;
        }
    }

    /// <summary>
    /// 远程玩家拾取物品事件
    /// </summary>
    public class RemoteItemPickedUpEvent : EventBase
    {
        public uint DropId { get; }
        public string PickedByPlayerId { get; }

        public RemoteItemPickedUpEvent(uint dropId, string pickedByPlayerId)
        {
            DropId = dropId;
            PickedByPlayerId = pickedByPlayerId;
        }
    }
}
