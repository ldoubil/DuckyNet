using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 动画同步客户端服务实现
    /// 接收远程玩家动画状态并应用到对应角色
    /// </summary>
    public class AnimatorSyncClientServiceImpl : IAnimatorSyncClientService
    {
        public void OnAnimatorStateUpdated(string steamId, AnimatorSyncData animatorData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteAnimatorUpdateEvent(steamId, animatorData));
            }
        }
    }
}
