using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 请求启动同步事件
    /// </summary>
    public class SyncStartRequestEvent
    {
        public static SyncStartRequestEvent Instance { get; } = new SyncStartRequestEvent();
        private SyncStartRequestEvent() { }
    }

    /// <summary>
    /// 请求停止同步事件
    /// </summary>
    public class SyncStopRequestEvent
    {
        public static SyncStopRequestEvent Instance { get; } = new SyncStopRequestEvent();
        private SyncStopRequestEvent() { }
    }

    /// <summary>
    /// 玩家位置同步事件
    /// 当接收到其他玩家的位置同步数据时触发此事件
    /// </summary>
    public class PlayerUnitySyncEvent
    {
        /// <summary>
        /// 其他玩家的位置同步数据（包含位置、旋转、速度等）
        /// </summary>
        public UnitySyncData SyncData { get; }
        public string SteamID { get; }

        public PlayerUnitySyncEvent(string steamID, UnitySyncData syncData)
        {
            SteamID = steamID;
            SyncData = syncData;
        }
    }

    /// <summary>
    /// 远程玩家动画更新事件
    /// 当接收到其他玩家的动画同步数据时触发此事件
    /// </summary>
    public class RemoteAnimatorUpdateEvent
    {
        /// <summary>
        /// 玩家ID
        /// </summary>
        public string PlayerId { get; }
        
        /// <summary>
        /// 动画同步数据
        /// </summary>
        public AnimatorSyncData AnimatorData { get; }

        public RemoteAnimatorUpdateEvent(string playerId, AnimatorSyncData animatorData)
        {
            PlayerId = playerId;
            AnimatorData = animatorData;
        }
    }
}

