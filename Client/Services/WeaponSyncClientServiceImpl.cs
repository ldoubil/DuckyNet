using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 武器同步客户端服务实现
    /// 接收来自服务器的武器同步通知
    /// </summary>
    public class WeaponSyncClientServiceImpl : IWeaponSyncClientService
    {
        /// <summary>
        /// 接收其他玩家的武器槽位更新通知
        /// </summary>
        public void OnWeaponSlotUpdated(WeaponSlotUpdateNotification notification)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteWeaponSlotUpdatedEvent(notification));
            }
        }

        /// <summary>
        /// 接收所有玩家的武器数据（加入房间时）
        /// </summary>
        public void OnAllPlayersWeaponReceived(AllPlayersWeaponData allWeaponData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new AllPlayersWeaponReceivedEvent(allWeaponData));
            }
        }

        /// <summary>
        /// 接收其他玩家的武器切换通知
        /// </summary>
        public void OnWeaponSwitched(WeaponSwitchNotification notification)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteWeaponSwitchedEvent(notification));
            }
        }

        /// <summary>
        /// 接收其他玩家的开枪特效通知
        /// </summary>
        public void OnWeaponFired(WeaponFireData fireData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteWeaponFiredEvent(fireData));
            }
        }
    }
}
