using System;
using System.Collections.Generic;
using System.Linq;
using DuckyNet.Server.RPC;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;

namespace DuckyNet.Server.Managers
{
    /// <summary>
    /// 广播管理器
    /// 统一管理所有类型的消息广播，避免重复代码
    /// </summary>
    public class BroadcastManager
    {
        private readonly RpcServer _server;
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;
        private readonly SceneManager _sceneManager;

        public BroadcastManager(
            RpcServer server, 
            PlayerManager playerManager, 
            RoomManager roomManager, 
            SceneManager sceneManager)
        {
            _server = server;
            _playerManager = playerManager;
            _roomManager = roomManager;
            _sceneManager = sceneManager;
        }

        /// <summary>
        /// 广播到房间内所有玩家（包括自己）
        /// </summary>
        public void BroadcastToRoom(PlayerInfo player, Action<PlayerInfo, IClientContext> action)
        {
            var room = _roomManager.GetPlayerRoom(player);
            if (room == null)
            {
                Console.WriteLine($"[BroadcastManager] ⚠️ 玩家 {player.SteamName} 不在任何房间中");
                return;
            }

            var roomPlayers = _roomManager.GetRoomPlayers(room.RoomId);
            BroadcastToPlayers(roomPlayers, action);
        }

        /// <summary>
        /// 广播到房间内所有其他玩家（不包括自己）
        /// </summary>
        public void BroadcastToRoomExcludeSelf(PlayerInfo player, Action<PlayerInfo, IClientContext> action)
        {
            var room = _roomManager.GetPlayerRoom(player);
            if (room == null)
            {
                Console.WriteLine($"[BroadcastManager] ⚠️ 玩家 {player.SteamName} 不在任何房间中");
                return;
            }

            var roomPlayers = _roomManager.GetRoomPlayers(room.RoomId)
                .Where(p => p.SteamId != player.SteamId)
                .ToArray();
            
            BroadcastToPlayers(roomPlayers, action);
        }

        /// <summary>
        /// 广播到同场景的玩家（同房间+同场景，不包括自己）
        /// </summary>
        public void BroadcastToScene(PlayerInfo player, Action<PlayerInfo, IClientContext> action)
        {
            var otherPlayers = _sceneManager.GetOtherPlayersInSameScene(player);
            BroadcastToPlayers(otherPlayers, action);
        }

        /// <summary>
        /// 广播到同场景的玩家（同房间+同场景，包括自己）
        /// </summary>
        public void BroadcastToSceneIncludeSelf(PlayerInfo player, Action<PlayerInfo, IClientContext> action)
        {
            var room = _roomManager.GetPlayerRoom(player);
            if (room == null)
            {
                Console.WriteLine($"[BroadcastManager] ⚠️ 玩家 {player.SteamName} 不在任何房间中");
                return;
            }

            var roomPlayers = _roomManager.GetRoomPlayers(room.RoomId)
                .Where(p => _sceneManager.IsSameScene(player, p))
                .ToArray();
            
            BroadcastToPlayers(roomPlayers, action);
        }

        /// <summary>
        /// 广播到指定房间的所有玩家
        /// </summary>
        public void BroadcastToRoomById(string roomId, Action<PlayerInfo, IClientContext> action)
        {
            var roomPlayers = _roomManager.GetRoomPlayers(roomId);
            BroadcastToPlayers(roomPlayers, action);
        }

        /// <summary>
        /// 广播到指定房间的所有玩家，排除某个玩家
        /// </summary>
        public void BroadcastToRoomByIdExclude(string roomId, string excludeSteamId, Action<PlayerInfo, IClientContext> action)
        {
            var roomPlayers = _roomManager.GetRoomPlayers(roomId)
                .Where(p => p.SteamId != excludeSteamId)
                .ToArray();
            
            BroadcastToPlayers(roomPlayers, action);
        }

        /// <summary>
        /// 获取房间内所有玩家的 ClientId 列表（排除自己）
        /// </summary>
        public List<string> GetRoomClientIds(PlayerInfo player, bool excludeSelf = true)
        {
            var room = _roomManager.GetPlayerRoom(player);
            if (room == null)
            {
                return new List<string>();
            }

            var roomPlayers = _roomManager.GetRoomPlayers(room.RoomId);
            return GetClientIdsFromPlayers(roomPlayers, excludeSelf ? player.SteamId : null);
        }

        /// <summary>
        /// 获取同场景玩家的 ClientId 列表（排除自己）
        /// </summary>
        public List<string> GetSceneClientIds(PlayerInfo player, bool excludeSelf = true)
        {
            var otherPlayers = _sceneManager.GetOtherPlayersInSameScene(player);
            return GetClientIdsFromPlayers(otherPlayers, excludeSelf ? player.SteamId : null);
        }

        /// <summary>
        /// 使用强类型广播到房间（排除自己）
        /// 例如：BroadcastToRoomTyped<IWeaponSyncClientService>(player, (service) => service.OnWeaponFired(data))
        /// </summary>
        public void BroadcastToRoomTyped<TService>(PlayerInfo player, Action<TService> action, bool excludeSelf = true) 
            where TService : class
        {
            var clientIds = GetRoomClientIds(player, excludeSelf);
            if (clientIds.Count > 0)
            {
                var proxy = _server.BroadcastToClients<TService>(clientIds);
                action(proxy);
            }
        }

        /// <summary>
        /// 使用强类型广播到场景（排除自己）
        /// </summary>
        public void BroadcastToSceneTyped<TService>(PlayerInfo player, Action<TService> action, bool excludeSelf = true) 
            where TService : class
        {
            var clientIds = GetSceneClientIds(player, excludeSelf);
            if (clientIds.Count > 0)
            {
                var proxy = _server.BroadcastToClients<TService>(clientIds);
                action(proxy);
            }
        }

        /// <summary>
        /// 发送给单个玩家（通过 SteamId）
        /// </summary>
        public void SendToPlayer(string steamId, Action<IClientContext> action)
        {
            var clientId = _playerManager.GetClientIdBySteamId(steamId);
            if (string.IsNullOrEmpty(clientId))
            {
                Console.WriteLine($"[BroadcastManager] ⚠️ 未找到玩家的 ClientId: {steamId}");
                return;
            }

            var context = _server.GetClientContext(clientId);
            if (context != null)
            {
                action(context);
            }
            else
            {
                Console.WriteLine($"[BroadcastManager] ⚠️ 未找到客户端上下文: {clientId}");
            }
        }

        /// <summary>
        /// 核心方法：广播到玩家列表
        /// </summary>
        private void BroadcastToPlayers(PlayerInfo[] players, Action<PlayerInfo, IClientContext> action)
        {
            foreach (var targetPlayer in players)
            {
                var clientId = _playerManager.GetClientIdBySteamId(targetPlayer.SteamId);
                if (string.IsNullOrEmpty(clientId))
                {
                    Console.WriteLine($"[BroadcastManager] ⚠️ 无法获取 ClientId: {targetPlayer.SteamName}({targetPlayer.SteamId})");
                    continue;
                }

                var context = _server.GetClientContext(clientId);
                if (context == null)
                {
                    Console.WriteLine($"[BroadcastManager] ⚠️ 客户端上下文为 null: {targetPlayer.SteamName}({clientId})");
                    continue;
                }

                try
                {
                    action(targetPlayer, context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BroadcastManager] ❌ 广播失败 {targetPlayer.SteamName}: {ex.Message}");
                    Console.WriteLine($"[BroadcastManager] 堆栈跟踪: {ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// 单个客户端调用（强类型）
        /// </summary>
        public void CallClientTyped<TService>(PlayerInfo player, Action<TService> action)
            where TService : class
        {
            var clientId = _playerManager.GetClientIdBySteamId(player.SteamId);
            if (string.IsNullOrEmpty(clientId))
            {
                Console.WriteLine($"[BroadcastManager] ⚠️ 未找到玩家的 ClientId: {player.SteamName}");
                return;
            }

            var clientIds = new List<string> { clientId };
            var proxy = _server.BroadcastToClients<TService>(clientIds);
            action(proxy);
        }

        /// <summary>
        /// 从玩家列表获取 ClientId 列表
        /// </summary>
        private List<string> GetClientIdsFromPlayers(PlayerInfo[] players, string? excludeSteamId = null)
        {
            var clientIds = new List<string>();
            
            foreach (var player in players)
            {
                if (!string.IsNullOrEmpty(excludeSteamId) && player.SteamId == excludeSteamId)
                {
                    continue;
                }

                var clientId = _playerManager.GetClientIdBySteamId(player.SteamId);
                if (!string.IsNullOrEmpty(clientId))
                {
                    clientIds.Add(clientId);
                }
            }

            return clientIds;
        }

        private float Distance(Vector3Data a, Vector3Data b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}

