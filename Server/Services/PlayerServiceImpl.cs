using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Managers;
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
        private readonly object _lock = new object();

        public PlayerServiceImpl(RpcServer server, PlayerManager playerManager)
        {
            _server = server;
            _playerManager = playerManager;
        }

        public async Task<LoginResult> LoginAsync(IClientContext client, PlayerInfo playerInfo)
        {
            Console.WriteLine($"[PlayerService] Login request from {playerInfo.SteamName} ({playerInfo.SteamId})");

            var result = _playerManager.OnClientLogin(client.ClientId, playerInfo);

            if (result.Success)
            {
                // 通知其他玩家（强类型广播）
                _server.Broadcast<IPlayerClientService>().OnPlayerJoined(playerInfo);
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
            }
        }

        public void SendChatMessage(IClientContext client, string message)
        {
            var player = _playerManager.GetPlayer(client.ClientId);
            if (player != null)
            {
                Console.WriteLine($"[Chat] {player.SteamName}: {message}");
                // TODO: 根据玩家是否在房间中，发送到房间或全局
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

