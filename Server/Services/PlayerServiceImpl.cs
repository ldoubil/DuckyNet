using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Events;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 玩家服务实现
    /// </summary>
    public class PlayerServiceImpl : IPlayerService
    {
        private readonly RpcServer _server;
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;
        private readonly object _lock = new object();

        public PlayerServiceImpl(RpcServer server, PlayerManager playerManager, RoomManager roomManager)
        {
            _server = server;
            _playerManager = playerManager;
            _roomManager = roomManager;
        }

        public async Task<LoginResult> LoginAsync(IClientContext client, PlayerInfo playerInfo)
        {
            Console.WriteLine($"[PlayerService] Login request from {playerInfo.SteamName} ({playerInfo.SteamId})");

            var result = _playerManager.OnClientLogin(client.ClientId, playerInfo);

            if (result.Success)
            {
                // 通知其他玩家（强类型广播）
                _server.Broadcast<IPlayerClientService>().OnPlayerJoined(playerInfo);
                
                // 发布登录事件
                ServerEventPublisher.PublishPlayerLogin(client.ClientId, playerInfo);
            }

            return await Task.FromResult(result);
        }

        public void Logout(IClientContext client)
        {
            Console.WriteLine($"[PlayerService] Logout request from {client.ClientId}");
            
            var player = _playerManager.GetPlayer(client.ClientId);
            if (player != null)
            {
                _server.Broadcast<IPlayerClientService>().OnPlayerLeft(player);
                Console.WriteLine($"[PlayerService] Player logged out: {player.SteamName}");
                
                // 发布登出事件
                ServerEventPublisher.PublishPlayerLogout(client.ClientId, player);
            }
        }

        public void SendChatMessage(IClientContext client, string message)
        {
            var player = _playerManager.GetPlayer(client.ClientId);
            if (player == null)
            {
                Console.WriteLine($"[Chat] 玩家未找到: ClientId={client.ClientId}");
                return;
            }

            Console.WriteLine($"[Chat] {player.SteamName}: {message}");
            
            // 检查玩家是否在房间中
            var room = _roomManager.GetPlayerRoom(player);
            Console.WriteLine($"[Chat] 玩家所在房间: {(room != null ? room.RoomId : "null (不在房间)")}");
            
            if (room != null)
            {
                // 在房间中，广播到房间内所有玩家
                var roomPlayers = _roomManager.GetRoomPlayers(room.RoomId);
                Console.WriteLine($"[Chat] 房间玩家数: {roomPlayers.Length}");
                
                var clientIds = roomPlayers
                    .Select(p => {
                        var cid = _playerManager.GetClientIdBySteamId(p.SteamId);
                        Console.WriteLine($"[Chat]   玩家: {p.SteamName} (SteamId={p.SteamId}) -> ClientId={cid ?? "null"}");
                        return cid;
                    })
                    .Where(id => id != null)
                    .Cast<string>()
                    .ToList();
                
                Console.WriteLine($"[Chat] 广播到房间 {room.RoomId} ({clientIds.Count} 个客户端)");
                _server.BroadcastToClients<IPlayerClientService>(clientIds).OnChatMessage(player, message);
            }
            else
            {
                // 不在房间中，广播到全局
                Console.WriteLine($"[Chat] 广播到全局");
                _server.Broadcast<IPlayerClientService>().OnChatMessage(player, message);
            }
        }



        public async Task<PlayerInfo[]> GetAllOnlinePlayersAsync(IClientContext client)
        {
            if (!_playerManager.IsLoggedIn(client.ClientId))
            {
                return Array.Empty<PlayerInfo>();
            }

            var players = _playerManager.GetAllOnlinePlayers();
            Console.WriteLine($"[PlayerService] Returning {players.Length} global online players");
            return await Task.FromResult(players);
        }

        public async Task<PlayerInfo[]> GetCurrentRoomPlayersAsync(IClientContext client)
        {
            if (!_playerManager.IsLoggedIn(client.ClientId))
            {
                return Array.Empty<PlayerInfo>();
            }

            var players = _playerManager.GetCurrentRoomPlayers(client.ClientId);
            Console.WriteLine($"[PlayerService] Returning {players.Length} room players");
            return await Task.FromResult(players);
        }
    }
}

