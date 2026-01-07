using DuckyNet.Shared.Events;
using DuckyNet.Shared.Services;

namespace DuckyNet.Server.Events
{
    /// <summary>
    /// 玩家登录事件
    /// </summary>
    public class PlayerLoginEvent : EventBase
    {
        public string ClientId { get; set; } = string.Empty;
        public PlayerInfo Player { get; set; } = null!;
    }

    /// <summary>
    /// 玩家登出事件
    /// </summary>
    public class PlayerLogoutEvent : EventBase
    {
        public string ClientId { get; set; } = string.Empty;
        public PlayerInfo Player { get; set; } = null!;
    }

    /// <summary>
    /// 玩家连接事件
    /// </summary>
    public class PlayerConnectedEvent : EventBase
    {
        public string ClientId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 玩家断开事件
    /// </summary>
    public class PlayerDisconnectedEvent : EventBase
    {
        public string ClientId { get; set; } = string.Empty;
        public PlayerInfo? Player { get; set; }
    }

    /// <summary>
    /// 房间创建事件
    /// </summary>
    public class RoomCreatedEvent : EventBase
    {
        public RoomInfo Room { get; set; } = null!;
        public PlayerInfo Host { get; set; } = null!;
    }

    /// <summary>
    /// 玩家加入房间事件
    /// </summary>
    public class PlayerJoinedRoomEvent : EventBase
    {
        public RoomInfo Room { get; set; } = null!;
        public PlayerInfo Player { get; set; } = null!;
    }

    /// <summary>
    /// 玩家离开房间事件
    /// </summary>
    public class PlayerLeftRoomEvent : EventBase
    {
        public RoomInfo? Room { get; set; }
        public PlayerInfo Player { get; set; } = null!;
    }

    /// <summary>
    /// 房间删除事件
    /// </summary>
    public class RoomDeletedEvent : EventBase
    {
        public string RoomId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 服务器启动事件
    /// </summary>
    public class ServerStartedEvent : EventBase
    {
        public int Port { get; set; }
    }

    /// <summary>
    /// 服务器关闭事件
    /// </summary>
    public class ServerStoppingEvent : EventBase
    {
    }
}
