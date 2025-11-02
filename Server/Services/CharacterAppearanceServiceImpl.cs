using System;
using System.Collections.Generic;
using DuckyNet.Server.Managers;
using DuckyNet.Server.RPC;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 角色外观同步服务实现
    /// 管理玩家外观数据的存储和分发
    /// </summary>
    public class CharacterAppearanceServiceImpl : ICharacterAppearanceService
    {
        private readonly RpcServer _server;
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;
        private readonly Dictionary<string, CharacterAppearanceData> _appearanceCache;
        private readonly object _lock = new object();

        public CharacterAppearanceServiceImpl(RpcServer server, PlayerManager playerManager, RoomManager roomManager)
        {
            _server = server;
            _playerManager = playerManager;
            _roomManager = roomManager;
            _appearanceCache = new Dictionary<string, CharacterAppearanceData>();
        }

        public void UploadAppearance(IClientContext client, CharacterAppearanceData appearanceData)
        {
            var player = _playerManager.GetPlayer(client.ClientId);
            if (player == null)
            {
                Console.WriteLine($"[CharacterAppearanceService] UploadAppearance failed: Player not found for client {client.ClientId}");
                return;
            }

            var steamId = player.SteamId;
            Console.WriteLine($"[CharacterAppearanceService] Uploading appearance for {player.SteamName} ({steamId})");

            lock (_lock)
            {
                // 存储外观数据
                _appearanceCache[steamId] = appearanceData;
            }

            // 获取玩家所在房间
            var room = _roomManager.GetPlayerRoom(player);
            if (room != null)
            {
                // 获取房间内所有玩家
                var roomPlayers = _roomManager.GetRoomPlayers(room.RoomId);
                Console.WriteLine($"[CharacterAppearanceService] Broadcasting appearance to room {room.RoomName} ({roomPlayers.Length} players)");
                
                // 收集所有在线客户端ID
                var clientIds = new List<string>();
                foreach (var roomPlayer in roomPlayers)
                {
                    var targetClientId = _playerManager.GetClientIdBySteamId(roomPlayer.SteamId);
                    if (targetClientId != null)
                    {
                        clientIds.Add(targetClientId);
                    }
                }

                // 广播给房间内所有玩家（包括自己）
                if (clientIds.Count > 0)
                {
                    _server.BroadcastToClients<ICharacterAppearanceClientService>(clientIds)
                        .OnAppearanceReceived(steamId, appearanceData);
                }
            }
            else
            {
                Console.WriteLine($"[CharacterAppearanceService] Player {steamId} is not in any room, appearance stored but not broadcasted");
            }
        }

        public void RequestAppearance(IClientContext client, string targetSteamId)
        {
            var requester = _playerManager.GetPlayer(client.ClientId);
            if (requester == null)
            {
                Console.WriteLine($"[CharacterAppearanceService] RequestAppearance failed: Requester not found for client {client.ClientId}");
                return;
            }

            Console.WriteLine($"[CharacterAppearanceService] {requester.SteamName} requesting appearance for {targetSteamId}");

            CharacterAppearanceData? appearanceData = null;
            lock (_lock)
            {
                if (_appearanceCache.TryGetValue(targetSteamId, out var data))
                {
                    appearanceData = data;
                }
            }

            if (appearanceData != null)
            {
                Console.WriteLine($"[CharacterAppearanceService] Sending cached appearance for {targetSteamId}");
                
                // 使用 BroadcastToClients 发送给单个客户端
                _server.BroadcastToClients<ICharacterAppearanceClientService>(new[] { client.ClientId })
                    .OnAppearanceReceived(targetSteamId, appearanceData);
            }
            else
            {
                Console.WriteLine($"[CharacterAppearanceService] No appearance data found for {targetSteamId}");
            }
        }

        /// <summary>
        /// 清理玩家的外观数据（玩家离线时调用）
        /// </summary>
        public void ClearAppearance(string steamId)
        {
            lock (_lock)
            {
                if (_appearanceCache.Remove(steamId))
                {
                    Console.WriteLine($"[CharacterAppearanceService] Cleared appearance data for {steamId}");
                }
            }
        }
    }
}
