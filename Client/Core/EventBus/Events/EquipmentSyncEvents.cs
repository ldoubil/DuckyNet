using DuckyNet.Shared.Data;
using DuckyNet.Shared.Events;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 远程玩家装备槽位更新事件
    /// </summary>
    public class RemoteEquipmentSlotUpdatedEvent : EventBase
    {
        public EquipmentSlotUpdateNotification Notification { get; }

        public RemoteEquipmentSlotUpdatedEvent(EquipmentSlotUpdateNotification notification)
        {
            Notification = notification;
        }
    }

    /// <summary>
    /// 远程玩家批量装备数据事件
    /// </summary>
    public class AllPlayersEquipmentReceivedEvent : EventBase
    {
        public AllPlayersEquipmentData EquipmentData { get; }

        public AllPlayersEquipmentReceivedEvent(AllPlayersEquipmentData equipmentData)
        {
            EquipmentData = equipmentData;
        }
    }
}
