using DuckyNet.Shared.Services;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 玩家加入游戏事件
    /// 当有新玩家成功登录加入游戏时触发此事件
    /// </summary>
    public class PlayerJoinedEvent
    {
        /// <summary>
        /// 加入游戏的玩家信息
        /// </summary>
        public PlayerInfo Player { get; }
        
        public PlayerJoinedEvent(PlayerInfo player)
        {
            Player = player;
        }
    }

    /// <summary>
    /// 玩家离开游戏事件
    /// 当玩家登出或断开连接时触发此事件
    /// </summary>
    public class PlayerLeftEvent
    {
        /// <summary>
        /// 离开游戏的玩家信息
        /// </summary>
        public PlayerInfo Player { get; }
        
        public PlayerLeftEvent(PlayerInfo player)
        {
            Player = player;
        }
    }
}

