using DuckyNet.Shared.Services;

namespace DuckyNet.Server.Events
{
    /// <summary>
    /// 服务器事件发布助手
    /// 提供静态方法方便服务层发布事件
    /// </summary>
    public static class ServerEventPublisher
    {
        private static IEventBus? _eventBus;

        /// <summary>
        /// 初始化事件发布器
        /// </summary>
        public static void Initialize(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        /// <summary>
        /// 发布玩家登录事件
        /// </summary>
        public static void PublishPlayerLogin(string clientId, PlayerInfo player)
        {
            _eventBus?.Publish(new PlayerLoginEvent
            {
                ClientId = clientId,
                Player = player
            });
        }

        /// <summary>
        /// 发布玩家登出事件
        /// </summary>
        public static void PublishPlayerLogout(string clientId, PlayerInfo player)
        {
            _eventBus?.Publish(new PlayerLogoutEvent
            {
                ClientId = clientId,
                Player = player
            });
        }

        /// <summary>
        /// 发布房间创建事件
        /// </summary>
        public static void PublishRoomCreated(RoomInfo room, PlayerInfo host)
        {
            _eventBus?.Publish(new RoomCreatedEvent
            {
                Room = room,
                Host = host
            });
        }

        /// <summary>
        /// 发布玩家加入房间事件
        /// </summary>
        public static void PublishPlayerJoinedRoom(RoomInfo room, PlayerInfo player)
        {
            _eventBus?.Publish(new PlayerJoinedRoomEvent
            {
                Room = room,
                Player = player
            });
        }

        /// <summary>
        /// 发布玩家离开房间事件
        /// </summary>
        public static void PublishPlayerLeftRoom(RoomInfo? room, PlayerInfo player)
        {
            _eventBus?.Publish(new PlayerLeftRoomEvent
            {
                Room = room,
                Player = player
            });
        }

        /// <summary>
        /// 发布房间删除事件
        /// </summary>
        public static void PublishRoomDeleted(string roomId)
        {
            _eventBus?.Publish(new RoomDeletedEvent
            {
                RoomId = roomId
            });
        }
    }
}

