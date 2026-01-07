using DuckyNet.Shared.Events;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 加入房间事件（自己的加入事件）
    /// </summary>
    public class RoomJoinedEvent : EventBase
    {
        public PlayerInfo Player { get; }
        public RoomInfo Room { get; }
        
        public RoomJoinedEvent(PlayerInfo player, RoomInfo room)
        {
            Player = player;
            Room = room;
        }
    }

    /// <summary>
    /// 离开房间事件（自己的离开事件）
    /// </summary>
    public class RoomLeftEvent : EventBase
    {
        public PlayerInfo Player { get; }
        public RoomInfo Room { get; }
        
        public RoomLeftEvent(PlayerInfo player, RoomInfo room)
        {
            Player = player;
            Room = room;
        }
    }
    /// <summary>
    /// 被踢出房间事件
    /// </summary>
    public class KickedFromRoomEvent : EventBase
    {
        public string Reason { get; }
        
        public KickedFromRoomEvent(string reason)
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// 玩家加入房间事件（其他玩家加入房间）
    /// </summary>
    public class PlayerJoinedRoomEvent : EventBase
    {
        public PlayerInfo Player { get; }
        public RoomInfo Room { get; }
        
        public PlayerJoinedRoomEvent(PlayerInfo player, RoomInfo room)
        {
            Player = player;
            Room = room;
        }
    }

    /// <summary>
    /// 玩家离开房间事件（其他玩家离开房间）
    /// </summary>
    public class PlayerLeftRoomEvent : EventBase
    {
        public PlayerInfo Player { get; }
        public RoomInfo Room { get; }
        
        public PlayerLeftRoomEvent(PlayerInfo player, RoomInfo room)
        {
            Player = player;
            Room = room;
        }
    }
}
