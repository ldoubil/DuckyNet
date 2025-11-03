using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 场景加载完成事件（包含子场景ID）
    /// </summary>
    public class SceneLoadedDetailEvent
    {
        public ScenelData ScenelData { get; }
        
        public SceneLoadedDetailEvent(ScenelData scenelData)
        {
            ScenelData = scenelData;
        }
    }

    /// <summary>
    /// 场景卸载事件（包含子场景ID）
    /// </summary>
    public class SceneUnloadingDetailEvent
    {
        public ScenelData ScenelData { get; }
        
        public SceneUnloadingDetailEvent(ScenelData scenelData)
        {
            ScenelData = scenelData;
        }
    }

    /// <summary>
    /// 网络通知玩家进入场景事件
    /// </summary>
    public class PlayerEnteredSceneEvent
    {
        public PlayerInfo PlayerInfo { get; }
        public ScenelData ScenelData { get; }
        
        public PlayerEnteredSceneEvent(PlayerInfo playerInfo, ScenelData scenelData)
        {
            PlayerInfo = playerInfo;
            ScenelData = scenelData;
        }
    }

    /// <summary>
    /// 网络通知玩家离开场景事件
    /// </summary>
    public class PlayerLeftSceneEvent
    {
        public PlayerInfo PlayerInfo { get; }
        public ScenelData ScenelData { get; }
        
        public PlayerLeftSceneEvent(PlayerInfo playerInfo, ScenelData scenelData)
        {
            PlayerInfo = playerInfo;
            ScenelData = scenelData;
        }
    }
}

