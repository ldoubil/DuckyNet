using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Server.Core;
using DuckyNet.Server.Events;
using DuckyNet.RPC;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Extensions;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Services;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 玩家服务实现
    /// </summary>
    public class PlayerServiceImpl : IPlayerService
    {
        public async Task<LoginResult> LoginAsync(IClientContext client, PlayerInfo playerInfo)
        {
            Console.WriteLine($"[PlayerService] Login request from {playerInfo.SteamName} ({playerInfo.SteamId})");
            Console.WriteLine($"[PlayerService] AvatarUrl: {playerInfo.AvatarUrl ?? "(null)"}");

            var result = ServerContext.Players.OnClientLogin(client.ClientId, playerInfo);

            if (result.Success)
            {
                // 通知所有在线玩家（全局广播）
                var allPlayers = ServerContext.Players.GetAllOnlinePlayers();
                var clientIds = allPlayers
                    .Select(p => ServerContext.Players.GetClientIdBySteamId(p.SteamId))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .ToList();
                
                if (clientIds.Count > 0)
                {
                    ServerContext.Server.SendTo<IPlayerClientService>(clientIds)
                        .OnPlayerJoined(playerInfo);
                }
                
                // 发布登录事件
                ServerEventPublisher.PublishPlayerLogin(client.ClientId, playerInfo);
            }

            return await Task.FromResult(result);
        }

        public void Logout(IClientContext client)
        {
            Console.WriteLine($"[PlayerService] Logout request from {client.ClientId}");
            
            var player = ServerContext.Players.GetPlayer(client.ClientId);
            if (player != null)
            {
                // 通知所有在线玩家
                var allPlayers = ServerContext.Players.GetAllOnlinePlayers();
                var clientIds = allPlayers
                    .Select(p => ServerContext.Players.GetClientIdBySteamId(p.SteamId))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .ToList();
                
                if (clientIds.Count > 0)
                {
                    ServerContext.Server.SendTo<IPlayerClientService>(clientIds)
                        .OnPlayerLeft(player);
                }
                
                Console.WriteLine($"[PlayerService] Player logged out: {player.SteamName}");
                
                // 发布登出事件
                ServerEventPublisher.PublishPlayerLogout(client.ClientId, player);
            }
        }

        public void SendChatMessage(IClientContext client, string message)
        {
            var player = ServerContext.Players.GetPlayer(client.ClientId);
            if (player == null)
            {
                Console.WriteLine($"[Chat] 玩家未找到: ClientId={client.ClientId}");
                return;
            }

            Console.WriteLine($"[Chat] {player.SteamName}: {message}");
            
            // 检查玩家是否在房间中
            var room = ServerContext.Rooms.GetPlayerRoom(player);
            
            if (room != null)
            {
                // 在房间中，广播到房间内所有玩家（包括自己）
                Console.WriteLine($"[Chat] 广播到房间 {room.RoomId}");
                var roomPlayers = ServerContext.Rooms.GetRoomPlayers(room.RoomId);
                var roomClientIds = roomPlayers
                    .Select(p => ServerContext.Players.GetClientIdBySteamId(p.SteamId))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .ToList();
                
                if (roomClientIds.Count > 0)
                {
                    ServerContext.Server.SendTo<IPlayerClientService>(roomClientIds)
                        .OnChatMessage(player, message);
                }
            }
            else
            {
                // 不在房间中，广播到全局所有玩家
                Console.WriteLine($"[Chat] 广播到全局");
                var allPlayers = ServerContext.Players.GetAllOnlinePlayers();
                var clientIds = allPlayers
                    .Select(p => ServerContext.Players.GetClientIdBySteamId(p.SteamId))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .ToList();
                
                if (clientIds.Count > 0)
                {
                    ServerContext.Server.SendTo<IPlayerClientService>(clientIds)
                        .OnChatMessage(player, message);
                }
            }
        }

        public async Task<PlayerInfo[]> GetAllOnlinePlayersAsync(IClientContext client)
        {
            var player = ServerContext.Players.GetPlayer(client.ClientId);
            if (player == null || !ServerContext.Players.IsLoggedIn(player.SteamId))
            {
                return Array.Empty<PlayerInfo>();
            }

            var players = ServerContext.Players.GetAllOnlinePlayers();
            Console.WriteLine($"[PlayerService] Returning {players.Length} global online players");
            return await Task.FromResult(players);
        }

        public async Task<PlayerInfo[]> GetCurrentRoomPlayersAsync(IClientContext client)
        {
            var player = ServerContext.Players.GetPlayer(client.ClientId);
            if (player == null || !ServerContext.Players.IsLoggedIn(player.SteamId))
            {
                return Array.Empty<PlayerInfo>();
            }

            var players = ServerContext.Players.GetCurrentRoomPlayers(client.ClientId);
            Console.WriteLine($"[PlayerService] Returning {players.Length} room players");
            return await Task.FromResult(players);
        }
    }
}
