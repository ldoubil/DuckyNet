using System;
using System.Collections.Generic;
using DuckyNet.Server.Core;
using DuckyNet.Server.Events;
using DuckyNet.Shared.Data;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Services;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 角色外观同步服务实现
    /// 管理玩家外观数据的存储和分发
    /// </summary>
    public class CharacterAppearanceServiceImpl : ICharacterAppearanceService
    {
        private readonly Dictionary<string, CharacterAppearanceData> _appearanceCache;
        private readonly object _lock = new object();

        public CharacterAppearanceServiceImpl(EventBus eventBus)
        {
            _appearanceCache = new Dictionary<string, CharacterAppearanceData>();
            
            // 订阅玩家断开事件，自动清理缓存
            eventBus.Subscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
        }

        /// <summary>
        /// 处理玩家断开事件：自动清理外观缓存
        /// </summary>
        private void OnPlayerDisconnected(PlayerDisconnectedEvent evt)
        {
            if (evt.Player != null)
            {
                ClearAppearance(evt.Player.SteamId);
            }
        }

        public void UploadAppearance(IClientContext client, CharacterAppearanceData appearanceData)
        {
            var player = ServerContext.Players.GetPlayer(client.ClientId);
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
            var room = ServerContext.Rooms.GetPlayerRoom(player);
            if (room != null)
            {
                // 使用 BroadcastManager 广播（包括自己）
                Console.WriteLine($"[CharacterAppearanceService] Broadcasting appearance to room {room.RoomName}");
                
                ServerContext.Broadcast.BroadcastToRoomTyped<ICharacterAppearanceClientService>(
                    player, 
                    service => service.OnAppearanceReceived(steamId, appearanceData),
                    excludeSelf: false);
            }
            else
            {
                Console.WriteLine($"[CharacterAppearanceService] Player {steamId} is not in any room, appearance stored but not broadcasted");
            }
        }

        public void RequestAppearance(IClientContext client, string targetSteamId)
        {
            var requester = ServerContext.Players.GetPlayer(client.ClientId);
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
                
                // 直接发送给请求客户端
                client.Call<ICharacterAppearanceClientService>()
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
