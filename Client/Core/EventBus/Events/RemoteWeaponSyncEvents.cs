using DuckyNet.Shared.Data;
using DuckyNet.Shared.Events;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 远程玩家武器槽位更新事件
    /// </summary>
    public class RemoteWeaponSlotUpdatedEvent : EventBase
    {
        public WeaponSlotUpdateNotification Notification { get; }

        public RemoteWeaponSlotUpdatedEvent(WeaponSlotUpdateNotification notification)
        {
            Notification = notification;
        }
    }

    /// <summary>
    /// 远程玩家批量武器数据事件
    /// </summary>
    public class AllPlayersWeaponReceivedEvent : EventBase
    {
        public AllPlayersWeaponData WeaponData { get; }

        public AllPlayersWeaponReceivedEvent(AllPlayersWeaponData weaponData)
        {
            WeaponData = weaponData;
        }
    }

    /// <summary>
    /// 远程玩家武器切换事件
    /// </summary>
    public class RemoteWeaponSwitchedEvent : EventBase
    {
        public WeaponSwitchNotification Notification { get; }

        public RemoteWeaponSwitchedEvent(WeaponSwitchNotification notification)
        {
            Notification = notification;
        }
    }

    /// <summary>
    /// 远程玩家武器开火特效事件
    /// </summary>
    public class RemoteWeaponFiredEvent : EventBase
    {
        public WeaponFireData FireData { get; }

        public RemoteWeaponFiredEvent(WeaponFireData fireData)
        {
            FireData = fireData;
        }
    }
}
