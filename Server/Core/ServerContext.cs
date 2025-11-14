using System;
using DuckyNet.RPC;
using DuckyNet.RPC.Core;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Events;

namespace DuckyNet.Server.Core
{
    /// <summary>
    /// 服务器全局上下文
    /// 提供对核心管理器和服务的全局访问
    /// </summary>
    public static class ServerContext
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// RPC 服务器实例
        /// </summary>
        public static RpcServer Server { get; private set; } = null!;

        /// <summary>
        /// 玩家管理器
        /// </summary>
        public static PlayerManager Players { get; private set; } = null!;

        /// <summary>
        /// 房间管理器
        /// </summary>
        public static RoomManager Rooms { get; private set; } = null!;

        /// <summary>
        /// 场景管理器
        /// </summary>
        public static SceneManager Scenes { get; private set; } = null!;

        /// <summary>
        /// 广播管理器
        /// </summary>
        public static BroadcastManager Broadcast { get; private set; } = null!;

        /// <summary>
        /// 事件总线
        /// </summary>
        public static EventBus Events { get; private set; } = null!;

        /// <summary>
        /// 初始化服务器上下文
        /// </summary>
        public static void Initialize(
            RpcServer server,
            PlayerManager playerManager,
            RoomManager roomManager,
            SceneManager sceneManager,
            BroadcastManager broadcastManager,
            EventBus eventBus)
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("ServerContext 已经初始化过了");
            }

            Server = server ?? throw new ArgumentNullException(nameof(server));
            Players = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
            Rooms = roomManager ?? throw new ArgumentNullException(nameof(roomManager));
            Scenes = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));
            Broadcast = broadcastManager ?? throw new ArgumentNullException(nameof(broadcastManager));
            Events = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            _isInitialized = true;

            // 初始化 ServerEventPublisher（保持向后兼容）
            ServerEventPublisher.Initialize(eventBus);

            Console.WriteLine("[ServerContext] ✅ 服务器全局上下文已初始化");
        }

        /// <summary>
        /// 检查上下文是否已初始化
        /// </summary>
        public static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("ServerContext 尚未初始化，请先调用 Initialize()");
            }
        }

        /// <summary>
        /// 重置上下文（仅用于测试）
        /// </summary>
        public static void Reset()
        {
            _isInitialized = false;
            Server = null!;
            Players = null!;
            Rooms = null!;
            Scenes = null!;
            Broadcast = null!;
            Events = null!;
        }
    }
}

