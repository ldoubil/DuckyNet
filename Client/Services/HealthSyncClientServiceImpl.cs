using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 血量同步客户端服务实现类
    /// 负责处理服务器向客户端发送的血量同步数据
    /// </summary>
    public class HealthSyncClientServiceImpl : IHealthSyncClientService
    {
        /// <summary>
        /// 接收其他玩家的血量同步数据回调方法
        /// 由服务器调用，当房间/场景内的其他玩家血量变化时触发
        /// </summary>
        /// <param name="healthData">其他玩家的血量同步数据</param>
        public void OnHealthSyncReceived(HealthSyncData healthData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemotePlayerHealthSyncEvent(healthData));
            }
        }
    }
}
