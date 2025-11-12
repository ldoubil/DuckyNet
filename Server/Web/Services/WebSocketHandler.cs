using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using DuckyNet.Server.Managers;

namespace DuckyNet.Server.Web.Services
{
    /// <summary>
    /// WebSocket 处理器 - 实时推送服务器状态
    /// </summary>
    public class WebSocketHandler
    {
        private static readonly ConcurrentDictionary<string, WebSocket> _clients = new();
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;
        private readonly SceneManager _sceneManager;
        private readonly PlayerNpcManager _npcManager;
        
        private Timer? _broadcastTimer;
        private bool _isRunning;

        public WebSocketHandler(
            PlayerManager playerManager,
            RoomManager roomManager,
            SceneManager sceneManager,
            PlayerNpcManager npcManager)
        {
            _playerManager = playerManager;
            _roomManager = roomManager;
            _sceneManager = sceneManager;
            _npcManager = npcManager;
        }

        /// <summary>
        /// 启动WebSocket广播服务
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            _broadcastTimer = new Timer(BroadcastUpdate, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
            Console.WriteLine("[WebSocket] Broadcast service started");
        }

        /// <summary>
        /// 停止WebSocket广播服务
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _broadcastTimer?.Dispose();
            
            // 关闭所有连接
            foreach (var client in _clients.Values)
            {
                if (client.State == WebSocketState.Open)
                {
                    client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None).Wait();
                }
            }
            _clients.Clear();
            
            Console.WriteLine("[WebSocket] Broadcast service stopped");
        }

        /// <summary>
        /// 处理新的WebSocket连接
        /// </summary>
        public async Task HandleAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var clientId = Guid.NewGuid().ToString();
            
            _clients.TryAdd(clientId, webSocket);
            Console.WriteLine($"[WebSocket] Client connected: {clientId} (Total: {_clients.Count})");

            try
            {
                // 立即发送当前状态
                await SendOverviewAsync(webSocket);

                // 保持连接并接收消息
                var buffer = new byte[1024 * 4];
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), 
                        CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    // 可以处理客户端消息（暂时不需要）
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] Error: {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                if (webSocket.State != WebSocketState.Closed)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, 
                        "Connection closed", 
                        CancellationToken.None);
                }
                Console.WriteLine($"[WebSocket] Client disconnected: {clientId} (Total: {_clients.Count})");
            }
        }

        /// <summary>
        /// 定时广播更新
        /// </summary>
        private async void BroadcastUpdate(object? state)
        {
            if (_clients.IsEmpty) return;

            try
            {
                // 广播概览数据
                await BroadcastOverviewAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] Broadcast error: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送概览数据给单个客户端
        /// </summary>
        private async Task SendOverviewAsync(WebSocket webSocket)
        {
            var rooms = _roomManager.GetAllRooms();
            var players = _playerManager.GetAllOnlinePlayers();
            var npcStats = _npcManager.GetStats();

            var data = new
            {
                type = "overview",
                data = new
                {
                    onlinePlayers = players.Length,
                    totalRooms = rooms.Length,
                    totalNpcs = npcStats.TotalNpcs,
                    serverTime = DateTime.UtcNow,
                    uptime = "运行中"
                }
            };

            await SendJsonAsync(webSocket, data);
        }

        /// <summary>
        /// 广播概览数据给所有客户端
        /// </summary>
        private async Task BroadcastOverviewAsync()
        {
            var rooms = _roomManager.GetAllRooms();
            var players = _playerManager.GetAllOnlinePlayers();
            var npcStats = _npcManager.GetStats();

            var data = new
            {
                type = "overview",
                data = new
                {
                    onlinePlayers = players.Length,
                    totalRooms = rooms.Length,
                    totalNpcs = npcStats.TotalNpcs,
                    serverTime = DateTime.UtcNow,
                    uptime = "运行中"
                }
            };

            await BroadcastAsync(data);
        }

        /// <summary>
        /// 广播消息给所有客户端
        /// </summary>
        private async Task BroadcastAsync(object data)
        {
            var json = JsonSerializer.Serialize(data);
            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            var deadClients = new System.Collections.Generic.List<string>();

            foreach (var kvp in _clients)
            {
                try
                {
                    if (kvp.Value.State == WebSocketState.Open)
                    {
                        await kvp.Value.SendAsync(
                            segment, 
                            WebSocketMessageType.Text, 
                            true, 
                            CancellationToken.None);
                    }
                    else
                    {
                        deadClients.Add(kvp.Key);
                    }
                }
                catch
                {
                    deadClients.Add(kvp.Key);
                }
            }

            // 清理断开的连接
            foreach (var clientId in deadClients)
            {
                _clients.TryRemove(clientId, out _);
            }
        }

        /// <summary>
        /// 发送JSON消息给单个客户端
        /// </summary>
        private async Task SendJsonAsync(WebSocket webSocket, object data)
        {
            if (webSocket.State != WebSocketState.Open)
                return;

            var json = JsonSerializer.Serialize(data);
            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            await webSocket.SendAsync(
                segment, 
                WebSocketMessageType.Text, 
                true, 
                CancellationToken.None);
        }

        /// <summary>
        /// 获取当前连接数
        /// </summary>
        public int GetClientCount() => _clients.Count;
    }
}

