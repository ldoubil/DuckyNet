using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using DuckyNet.Server.Core;
using DuckyNet.RPC;
using DuckyNet.RPC.Core;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Plugin;
using DuckyNet.Server.Events;

#nullable enable

namespace DuckyNet.Server
{
    /// <summary>
    /// DuckyNet 服务器程序
    /// [REFACTOR] 阶段1：引入依赖注入容器
    /// </summary>
    class Program
    {
        private static IServiceProvider _serviceProvider = null!;
        private static RpcServer _server = null!;
        private static PlayerManager _playerManager = null!;
        private static EventBus _eventBus = null!;
        private static PluginManager _pluginManager = null!;
        private static WebApplication? _webApp = null;
        private static bool _running = true;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== DuckyNet Server ===");
            Console.WriteLine();

            try
            {
                // ========== 阶段1：配置依赖注入容器 ==========
                Console.WriteLine("[Server] Configuring services...");
                var services = new ServiceCollection();

                // 加载插件配置并注册插件
                var pluginConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.plugins.json");
                var pluginConfig = PluginManager.LoadConfiguration(pluginConfigPath);
                _pluginManager = new PluginManager(pluginConfig);
                _pluginManager.ConfigureServices(services);
                services.AddSingleton(_pluginManager);
                
                // 构建服务提供者
                _serviceProvider = services.BuildServiceProvider();
                Console.WriteLine("[Server] ✓ Services configured");

                // ========== 阶段2：初始化核心组件 ==========
                Console.WriteLine("[Server] Initializing components...");
                
                // 初始化静态依赖（过渡方案）
                ServerInitializer.InitializeStaticDependencies(_serviceProvider);
                
                // 获取核心服务实例
                _server = _serviceProvider.GetRequiredService<RpcServer>();
                _playerManager = _serviceProvider.GetRequiredService<PlayerManager>();
                _eventBus = _serviceProvider.GetRequiredService<EventBus>();
                
                // 初始化服务器上下文并注册所有 RPC 服务
                ServiceCollectionExtensions.InitializeServer(_serviceProvider);

                var pluginContext = new PluginContext(
                    _playerManager,
                    _serviceProvider.GetRequiredService<RoomManager>(),
                    _server,
                    _serviceProvider,
                    _eventBus,
                    new PluginLogger("System")
                );
                _pluginManager.Initialize(pluginContext);
                _pluginManager.LoadConfiguredPlugins(_server);
                Console.WriteLine("[Server] ✓ Components initialized");

                // ========== 阶段3：启动服务器 ==========

                // 订阅事件
                _server.ClientConnected += OnClientConnected;
                _server.ClientDisconnected += OnClientDisconnected;

                Console.WriteLine("[Server] Starting server...");
                
                // 启动 RPC 服务器
                int port = 9050;
                _server.Start(port);
                Console.WriteLine($"[Server] ✓ RPC Server listening on port {port}");
                Console.WriteLine($"[Server] ✓ Login timeout: 3 seconds");

                // 加载插件
                var pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                _pluginManager.LoadPluginsFromDirectory(pluginDir, _server);
                Console.WriteLine($"[Server] ✓ Plugins loaded from: {pluginDir}");
                Console.WriteLine();

                // 发布服务器启动事件
                _eventBus.Publish(new ServerStartedEvent { Port = port });

                // ========== 阶段4：启动Web服务器 ==========
                Console.WriteLine("[Server] Starting Web server...");
                var builder = WebApplication.CreateBuilder(args);
                _pluginManager.ConfigureWebServices(builder.Services);
                _webApp = builder.Build();
                _pluginManager.ConfigureWeb(_webApp);
                var webTask = _webApp.RunAsync("http://localhost:5000");
                Console.WriteLine("[Server] ✓ Web server started at http://localhost:5000");
                Console.WriteLine("[Server] ✓ Admin dashboard: http://localhost:5000");
                Console.WriteLine();

                // ========== 阶段5：启动后台任务 ==========
                var updateThread = new Thread(UpdateLoop);
                updateThread.IsBackground = true;
                updateThread.Start();

                var timeoutThread = new Thread(TimeoutCheckLoop);
                timeoutThread.IsBackground = true;
                timeoutThread.Start();
                Console.WriteLine("[Server] ✓ Background tasks started");
                Console.WriteLine();
                Console.WriteLine("==================================");
                Console.WriteLine("  Server is ready!");
                Console.WriteLine("  RPC Server: Port 9050");
                Console.WriteLine("  Web Admin: http://localhost:5000");
                Console.WriteLine("  Press Ctrl+C to stop server");
                Console.WriteLine("==================================");
                Console.WriteLine();
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
                
                // 停止 Web 服务器（设置短超时避免卡住）
                if (_webApp != null)
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    try
                    {
                        await _webApp.StopAsync(cts.Token);
                        Console.WriteLine("[Server] Web server stopped");
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("[Server] Web server force stopped (timeout)");
                    }
                }
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
