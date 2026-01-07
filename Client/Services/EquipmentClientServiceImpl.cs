using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 装备同步客户端服务实现
    /// 接收来自服务器的装备同步通知
    /// </summary>
    public class EquipmentClientServiceImpl : IEquipmentClientService
    {
        /// <summary>
        /// 接收其他玩家的装备槽位更新通知
        /// </summary>
        public void OnEquipmentSlotUpdated(EquipmentSlotUpdateNotification notification)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteEquipmentSlotUpdatedEvent(notification));
            }
        }

        /// <summary>
        /// 接收所有玩家的装备数据（加入房间时）
        /// </summary>
        public void OnAllPlayersEquipmentReceived(AllPlayersEquipmentData allEquipmentData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new AllPlayersEquipmentReceivedEvent(allEquipmentData));
            }
        }
    }
}
