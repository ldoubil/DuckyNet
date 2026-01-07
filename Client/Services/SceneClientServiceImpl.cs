using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 场景客户端服务实现
    /// </summary>
    public class SceneClientServiceImpl : ISceneClientService
    {
        /// <summary>
        /// 玩家进入场景通知（服务器调用）
        /// </summary>
        public void OnPlayerEnteredScene(PlayerInfo playerInfo, ScenelData scenelData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new PlayerEnteredSceneEvent(playerInfo, scenelData));
            }
        }
        
     

        /// <summary>
        /// 玩家离开场景通知（服务器调用）
        /// </summary>
        public void OnPlayerLeftScene(PlayerInfo playerInfo, ScenelData scenelData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new PlayerLeftSceneEvent(playerInfo, scenelData));
            }
        }
    }
}
