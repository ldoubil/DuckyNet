using System;
using System.Threading;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Services;
using DuckyNet.Server.Managers;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.RPC;

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

                // 创建管理器
                _roomManager = new RoomManager();
                _playerManager = new PlayerManager(_server, _roomManager);

                // 创建服务
                var playerService = new PlayerServiceImpl(_server, _playerManager);
                var roomService = new RoomServiceImpl(_server, _roomManager, _playerManager);

                // 注册服务
                _server.RegisterServerService<IPlayerService>(playerService);
                _server.RegisterServerService<IRoomService>(roomService);

                // 订阅事件
                _server.ClientConnected += OnClientConnected;
                _server.ClientDisconnected += OnClientDisconnected;

                // 启动服务器
                int port = 2025;
                _server.Start(port);
                Console.WriteLine($"[Server] Started on port {port}");
                Console.WriteLine("[Server] Login timeout: 3 seconds");
                Console.WriteLine();

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
        }

        static void OnClientDisconnected(string clientId)
        {
            _playerManager?.OnClientDisconnected(clientId);
            Console.WriteLine($"[Server] Client disconnected: {clientId}");
        }
    }
}
