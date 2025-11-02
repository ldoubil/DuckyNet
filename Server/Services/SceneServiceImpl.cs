using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.RPC;
using DuckyNet.Server.Managers;
using DuckyNet.Server.RPC;
using DuckyNet.Shared.Data;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 场景服务实现
    /// </summary>
    public class SceneServiceImpl : ISceneService
    {
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;
        private readonly RpcServer _server;


        public SceneServiceImpl(RpcServer server, PlayerManager playerManager, RoomManager roomManager)
        {
            _server = server;
            _playerManager = playerManager;
            _roomManager = roomManager;
        }

        /// <summary>
        /// 玩家进入场景
        /// 🔥 通知房间其他玩家：该玩家进入了场景
        /// </summary>
        public Task<bool> EnterSceneAsync(IClientContext client, ScenelData scenelData)
        {
            var nonNullData = scenelData ?? new ScenelData("", "");
            var player = _playerManager.GetPlayer(client.ClientId);
            
            if (player != null)
            {
                Console.WriteLine($"[SceneService] {player.SteamName} 进入场景: {nonNullData.SceneName}/{nonNullData.SubSceneName}");
                
                // 🔥 核心：更新玩家的场景数据（影响位置同步筛选）
                _playerManager.UpdatePlayerSceneDataByClientId(client.ClientId, nonNullData);
                
                // 🔥 通知房间内所有玩家（包括自己）：该玩家进入了场景
                var roomId = _roomManager.GetPlayerRoom(player)?.RoomId ?? "";
                if (!string.IsNullOrEmpty(roomId))
                {
                    var roomPlayers = _playerManager.GetRoomPlayers(roomId);
                    foreach (var p in roomPlayers)
                    {
                        var targetClientId = _playerManager.GetClientIdBySteamId(p.SteamId);
                        if (!string.IsNullOrEmpty(targetClientId))
                        {
                            var clientContext = _server.GetClientContext(targetClientId);
                            if (clientContext != null)
                            {
                                clientContext.Call<ISceneClientService>().OnPlayerEnteredScene(player, nonNullData);
                                Console.WriteLine($"[SceneService] ✅ 通知 {p.SteamName}: {player.SteamName} 进入场景 {nonNullData.SceneName}");
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"[SceneService] ⚠️ 未找到玩家信息, client.ClientId={client.ClientId}");
            }
            
            return Task.FromResult(true);
        }

        public Task<PlayerInfo[]> GetScenePlayersAsync(IClientContext client, ScenelData scenelData)
        {
            var playerInfo = _playerManager.GetPlayer(client.ClientId);
            if (playerInfo != null)
            {
                var roomId = _roomManager.GetPlayerRoom(playerInfo)?.RoomId ?? "";
                var players = _playerManager.GetRoomPlayers(roomId);
                // 匹配 scenelData.SceneName 和 scenelData.SubSceneName 
                var matchedPlayers = players.Where(p => p.CurrentScenelData.SceneName == scenelData.SceneName && p.CurrentScenelData.SubSceneName == scenelData.SubSceneName).ToArray();
                return Task.FromResult(matchedPlayers);
            }
            return Task.FromResult(Array.Empty<PlayerInfo>());
        }

        /// <summary>
        /// 玩家离开场景
        /// 🔥 必要：通知其他玩家销毁角色（RemotePlayer订阅OnPlayerLeftScene）
        /// </summary>
        public Task<bool> LeaveSceneAsync(IClientContext client, ScenelData scenelData)
        {
            var player = _playerManager.GetPlayer(client.ClientId);
            if (player != null)
            {
                Console.WriteLine($"[SceneService] {player.SteamName} 离开场景: {scenelData.SceneName}/{scenelData.SubSceneName}");
                
                // 🔥 清除玩家的场景数据（重要！影响位置同步过滤）
                _playerManager.UpdatePlayerSceneDataByClientId(client.ClientId, new ScenelData("", ""));
                
                // 🔥 通知房间内所有玩家（用于销毁角色）
                var roomId = _roomManager.GetPlayerRoom(player)?.RoomId ?? "";
                if (!string.IsNullOrEmpty(roomId))
                {
                    var roomPlayers = _playerManager.GetRoomPlayers(roomId);
                    foreach (var p in roomPlayers)
                    {
                        var targetClientId = _playerManager.GetClientIdBySteamId(p.SteamId);
                        if (!string.IsNullOrEmpty(targetClientId))
                        {
                            var clientContext = _server.GetClientContext(targetClientId);
                            if (clientContext != null)
                            {
                                clientContext.Call<ISceneClientService>().OnPlayerLeftScene(player, scenelData);
                                Console.WriteLine($"[SceneService] ✅ 通知 {p.SteamName}: {player.SteamName} 离开场景");
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"[SceneService] ⚠️ 未找到玩家信息, client.ClientId={client.ClientId}");
            }
            
            return Task.FromResult(true);
        }
    }
}
