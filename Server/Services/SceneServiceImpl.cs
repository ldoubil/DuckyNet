using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.RPC;
using DuckyNet.Server.Managers;
using DuckyNet.Server.RPC;

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

        /// <summary>
        /// 玩家场景映射（steamId -> sceneName）
        /// </summary>
        private readonly Dictionary<string, string> _playerScenes = new Dictionary<string, string>();

        /// <summary>
        /// 场景玩家映射（sceneName -> HashSet<steamId>）
        /// </summary>
        private readonly Dictionary<string, HashSet<string>> _scenePlayers = new Dictionary<string, HashSet<string>>();

        public SceneServiceImpl(RpcServer server, PlayerManager playerManager, RoomManager roomManager)
        {
            _server = server;
            _playerManager = playerManager;
            _roomManager = roomManager;
        }

        /// <summary>
        /// 进入场景
        /// </summary>
        public Task<bool> EnterSceneAsync(IClientContext client, string sceneName)
        {
            try
            {
                var player = _playerManager.GetPlayer(client.ClientId);
                if (player == null)
                {
                    Console.WriteLine($"[SceneService] 玩家未找到: {client.ClientId}");
                    return Task.FromResult(false);
                }

                // 检查玩家是否在房间中
                var room = _roomManager.GetPlayerRoom(player.SteamId);
                if (room == null)
                {
                    Console.WriteLine($"[SceneService] 玩家不在任何房间中: {player.SteamId}");
                    return Task.FromResult(false);
                }

                // 如果玩家已经在其他场景，先离开
                if (_playerScenes.TryGetValue(player.SteamId, out var oldScene))
                {
                    LeaveSceneInternal(player.SteamId, oldScene, room.RoomId);
                }

                // 进入新场景
                _playerScenes[player.SteamId] = sceneName;

                // 更新玩家的当前场景ID（地图名）
                player.CurrentSceneId = sceneName;

                if (!_scenePlayers.ContainsKey(sceneName))
                {
                    _scenePlayers[sceneName] = new HashSet<string>();
                }
                _scenePlayers[sceneName].Add(player.SteamId);

                Console.WriteLine($"[SceneService] 玩家进入场景: {player.SteamName} -> {sceneName} (CurrentSceneId已更新)");

                // 构造玩家场景信息
                var playerSceneInfo = new PlayerSceneInfo
                {
                    SteamId = player.SteamId,
                    PlayerInfo = player,
                    SceneName = sceneName,
                    HasCharacter = player.HasCharacter
                };

                // 通知房间内的所有其他玩家
                NotifyPlayerEnteredScene(room.RoomId, playerSceneInfo);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SceneService] 进入场景失败: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 离开当前场景
        /// </summary>
        public Task<bool> LeaveSceneAsync(IClientContext client)
        {
            try
            {
                var player = _playerManager.GetPlayer(client.ClientId);
                if (player == null)
                {
                    return Task.FromResult(false);
                }

                if (_playerScenes.TryGetValue(player.SteamId, out var sceneName))
                {
                    var room = _roomManager.GetPlayerRoom(player.SteamId);
                    if (room != null)
                    {
                        LeaveSceneInternal(player.SteamId, sceneName, room.RoomId);
                    }
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SceneService] 离开场景失败: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 获取场景内的玩家列表
        /// </summary>
        public Task<PlayerSceneInfo[]> GetScenePlayersAsync(IClientContext client, string sceneName)
        {
            try
            {
                var player = _playerManager.GetPlayer(client.ClientId);
                if (player == null)
                {
                    return Task.FromResult(Array.Empty<PlayerSceneInfo>());
                }

                var room = _roomManager.GetPlayerRoom(player.SteamId);
                if (room == null)
                {
                    return Task.FromResult(Array.Empty<PlayerSceneInfo>());
                }

                if (!_scenePlayers.TryGetValue(sceneName, out var steamIds))
                {
                    return Task.FromResult(Array.Empty<PlayerSceneInfo>());
                }

                var roomPlayerIds = _roomManager.GetRoomPlayerIds(room.RoomId);
                var result = new List<PlayerSceneInfo>();
                
                foreach (var steamId in steamIds)
                {
                    // 只返回同房间的玩家
                    if (!roomPlayerIds.Contains(steamId))
                        continue;

                    var p = _playerManager.GetPlayer(steamId);
                    if (p != null)
                    {
                        result.Add(new PlayerSceneInfo
                        {
                            SteamId = p.SteamId,
                            PlayerInfo = p,
                            SceneName = sceneName,
                            HasCharacter = p.HasCharacter
                        });
                    }
                }

                return Task.FromResult(result.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SceneService] 获取场景玩家失败: {ex.Message}");
                return Task.FromResult(Array.Empty<PlayerSceneInfo>());
            }
        }

        /// <summary>
        /// 获取当前场景信息
        /// </summary>
        public Task<PlayerSceneInfo?> GetCurrentSceneAsync(IClientContext client)
        {
            try
            {
                var player = _playerManager.GetPlayer(client.ClientId);
                if (player == null)
                {
                    return Task.FromResult<PlayerSceneInfo?>(null);
                }

                if (_playerScenes.TryGetValue(player.SteamId, out var sceneName))
                {
                    return Task.FromResult<PlayerSceneInfo?>(new PlayerSceneInfo
                    {
                        SteamId = player.SteamId,
                        PlayerInfo = player,
                        SceneName = sceneName,
                        HasCharacter = player.HasCharacter
                    });
                }
                
                return Task.FromResult<PlayerSceneInfo?>(null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SceneService] 获取当前场景失败: {ex.Message}");
                return Task.FromResult<PlayerSceneInfo?>(null);
            }
        }

        /// <summary>
        /// 获取所有玩家的场景信息（当前房间内）
        /// </summary>
        public Task<PlayerSceneInfo[]> GetAllPlayerScenesAsync(IClientContext client)
        {
            try
            {
                var player = _playerManager.GetPlayer(client.ClientId);
                if (player == null)
                {
                    return Task.FromResult(Array.Empty<PlayerSceneInfo>());
                }

                var room = _roomManager.GetPlayerRoom(player.SteamId);
                if (room == null)
                {
                    return Task.FromResult(Array.Empty<PlayerSceneInfo>());
                }

                var roomPlayerIds = _roomManager.GetRoomPlayerIds(room.RoomId);
                var result = new List<PlayerSceneInfo>();

                foreach (var steamId in roomPlayerIds)
                {
                    var roomPlayer = _playerManager.GetPlayer(steamId);
                    if (roomPlayer == null) continue;

                    _playerScenes.TryGetValue(roomPlayer.SteamId, out var sceneName);
                    
                    result.Add(new PlayerSceneInfo
                    {
                        SteamId = roomPlayer.SteamId,
                        PlayerInfo = roomPlayer,
                        SceneName = sceneName ?? string.Empty,
                        HasCharacter = roomPlayer.HasCharacter
                    });
                }

                return Task.FromResult(result.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SceneService] 获取所有玩家场景失败: {ex.Message}");
                return Task.FromResult(Array.Empty<PlayerSceneInfo>());
            }
        }

        /// <summary>
        /// 内部离开场景实现
        /// </summary>
        private void LeaveSceneInternal(string steamId, string sceneName, string roomId)
        {
            _playerScenes.Remove(steamId);

            // 清除玩家的当前场景ID（地图名）
            var player = _playerManager.GetPlayerBySteamId(steamId);
            if (player != null)
            {
                player.CurrentSceneId = string.Empty;
            }

            if (_scenePlayers.TryGetValue(sceneName, out var players))
            {
                players.Remove(steamId);
                if (players.Count == 0)
                {
                    _scenePlayers.Remove(sceneName);
                }
            }

            Console.WriteLine($"[SceneService] 玩家离开场景: {player?.SteamName ?? steamId} <- {sceneName} (CurrentSceneId已清除)");

            // 通知房间内的所有其他玩家
            NotifyPlayerLeftScene(roomId, steamId, sceneName);
        }

        /// <summary>
        /// 通知玩家进入场景
        /// </summary>
        private void NotifyPlayerEnteredScene(string roomId, PlayerSceneInfo playerSceneInfo)
        {
            var roomPlayerIds = _roomManager.GetRoomPlayerIds(roomId);
            foreach (var steamId in roomPlayerIds)
            {
                // 跳过自己
                if (steamId == playerSceneInfo.SteamId)
                    continue;

                try
                {
                    var clientContext = _server.GetClientContext(steamId);
                    if (clientContext != null)
                    {
                        clientContext.Call<ISceneClientService>()
                            .OnPlayerEnteredScene(playerSceneInfo);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SceneService] 通知玩家进入场景失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 通知玩家离开场景
        /// </summary>
        private void NotifyPlayerLeftScene(string roomId, string steamId, string sceneName)
        {
            var roomPlayerIds = _roomManager.GetRoomPlayerIds(roomId);
            foreach (var roomSteamId in roomPlayerIds)
            {
                // 跳过自己
                if (roomSteamId == steamId)
                    continue;

                try
                {
                    var clientContext = _server.GetClientContext(roomSteamId);
                    if (clientContext != null)
                    {
                        clientContext.Call<ISceneClientService>()
                            .OnPlayerLeftScene(steamId, sceneName);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SceneService] 通知玩家离开场景失败: {ex.Message}");
                }
            }
        }
    }
}

