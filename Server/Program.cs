using System;
using System.IO;
using System.Threading;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Services;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Plugin;
using DuckyNet.Server.Events;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.RPC;
using EventBus = DuckyNet.Server.Events.EventBus;

namespace DuckyNet.Server
{
    /// <summary>
    /// DuckyNet 服务器程序
    /// </summary>
    class Program
    {
        private static RpcServer _server = null!;
        private static PlayerManager _playerManager = null!;
        private static RoomManager _roomManager = null!;
        private static EventBus _eventBus = null!;
        private static PluginManager _pluginManager = null!;
        private static bool _running = true;

        static void Main(string[] args)
        {
            Console.WriteLine("=== DuckyNet Server ===");
            Console.WriteLine();

            try
            {
                // 创建服务器配置
                var config = RpcConfig.Development;
                _server = new RpcServer(config);

                // 创建事件总线
                _eventBus = new EventBus();
                ServerEventPublisher.Initialize(_eventBus);

                // 创建管理器
                _roomManager = new RoomManager();
                _playerManager = new PlayerManager(_server, _roomManager);

                // 初始化房间广播辅助类（用于 BroadcastToRoom 扩展方法）
                RPC.RoomBroadcastHelper.Initialize(_roomManager, _playerManager);

                // 创建服务（注意顺序：SceneService 需要在 CharacterService 之前创建）
                var playerService = new PlayerServiceImpl(_server, _playerManager, _roomManager);
                var playerUnitySyncService = new PlayerUnitySyncServiceImpl(_server, _playerManager, _roomManager);
                var healthSyncService = new HealthSyncServiceImpl(_server, _playerManager, _roomManager);
                var roomService = new RoomServiceImpl(_server, _roomManager, _playerManager, playerUnitySyncService);
                var sceneService = new SceneServiceImpl(_server, _playerManager, _roomManager);
                var characterService = new CharacterServiceImpl(_server, _playerManager, _roomManager, sceneService);
                var appearanceService = new CharacterAppearanceServiceImpl(_server, _playerManager, _roomManager);
                var animatorSyncService = new AnimatorSyncServiceImpl(_server, _playerManager, _roomManager);
                var itemSyncService = new ItemSyncServiceImpl(_server, _playerManager, _roomManager);
                var equipmentService = new EquipmentServerServiceImpl(_server, _playerManager, _roomManager);
                var weaponSyncService = new WeaponSyncServerServiceImpl(_server, _playerManager, _roomManager);
                
                // 设置装备和武器服务到 RoomService（延迟注入）
                roomService.SetEquipmentService(equipmentService);
                roomService.SetWeaponSyncService(weaponSyncService);

                // 注册服务
                _server.RegisterServerService<IPlayerService>(playerService);
                _server.RegisterServerService<IRoomService>(roomService);
                _server.RegisterServerService<ISceneService>(sceneService);
                _server.RegisterServerService<ICharacterService>(characterService);
                _server.RegisterServerService<IPlayerUnitySyncService>(playerUnitySyncService);
                _server.RegisterServerService<IHealthSyncService>(healthSyncService);
                _server.RegisterServerService<ICharacterAppearanceService>(appearanceService);
                _server.RegisterServerService<IAnimatorSyncService>(animatorSyncService);
                _server.RegisterServerService<IItemSyncService>(itemSyncService);
                _server.RegisterServerService<IEquipmentService>(equipmentService);
                _server.RegisterServerService<IWeaponSyncService>(weaponSyncService);

                // 订阅事件
                _server.ClientConnected += OnClientConnected;
                _server.ClientDisconnected += OnClientDisconnected;

                // 启动服务器
                int port = 9050;
                _server.Start(port);
                Console.WriteLine($"[Server] Started on port {port}");
                Console.WriteLine("[Server] Login timeout: 3 seconds");
                Console.WriteLine();

                // 创建插件上下文
                var pluginContext = new PluginContext(
                    _playerManager,
                    _roomManager,
                    _server,
                    _eventBus,
                    new PluginLogger("System")
                );

                // 创建插件管理器并加载插件
                _pluginManager = new PluginManager(pluginContext);
                var pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                _pluginManager.LoadPluginsFromDirectory(pluginDir);
                Console.WriteLine();

                // 发布服务器启动事件
                _eventBus.Publish(new ServerStartedEvent { Port = port });

                // 启动更新线程
                var updateThread = new Thread(UpdateLoop);
                updateThread.IsBackground = true;
                updateThread.Start();

                // 启动登录超时检查线程
                var timeoutThread = new Thread(TimeoutCheckLoop);
                timeoutThread.IsBackground = true;
                timeoutThread.Start();

                Console.WriteLine("Press Ctrl+C to stop server...");
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    _running = false;
                };

                // 主线程等待
                while (_running)
                {
                    Thread.Sleep(100);
                }

                Console.WriteLine("[Server] Shutting down...");
                _eventBus.Publish(new ServerStoppingEvent());
                _pluginManager.UnloadAllPlugins();
                _server.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void UpdateLoop()
        {
            while (_running)
            {
                _server?.Update();
                _pluginManager?.UpdatePlugins();
                Thread.Sleep(15); // ~60 FPS
            }
        }

        static void TimeoutCheckLoop()
        {
            while (_running)
            {
                _playerManager?.CheckLoginTimeouts();
                Thread.Sleep(1000); // 每秒检查一次
            }
        }

        static void OnClientConnected(string clientId)
        {
            _playerManager?.OnClientConnected(clientId);
            Console.WriteLine($"[Server] Client connected: {clientId}");
            _eventBus?.Publish(new PlayerConnectedEvent { ClientId = clientId });
        }

        static void OnClientDisconnected(string clientId)
        {
            var player = _playerManager?.GetPlayer(clientId);
            _playerManager?.OnClientDisconnected(clientId);
            Console.WriteLine($"[Server] Client disconnected: {clientId}");
            _eventBus?.Publish(new PlayerDisconnectedEvent 
            { 
                ClientId = clientId,
                Player = player
            });
        }
    }
}
