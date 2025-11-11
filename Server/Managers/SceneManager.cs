using System;
using System.Collections.Generic;
using System.Linq;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;

namespace DuckyNet.Server.Managers
{
    /// <summary>
    /// 场景管理器
    /// 负责管理玩家的场景状态和场景匹配逻辑
    /// 新增：缓存玩家位置用于热区和范围计算
    /// </summary>
    public class SceneManager
    {
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;
        private readonly object _lock = new object();

        // 玩家位置缓存 (SteamId -> Vector3Data)
        private readonly Dictionary<string, Vector3Data> _playerPositions = new Dictionary<string, Vector3Data>();

        public SceneManager(PlayerManager playerManager, RoomManager roomManager)
        {
            _playerManager = playerManager;
            _roomManager = roomManager;
        }

        /// <summary>
        /// 玩家进入场景
        /// </summary>
        public bool EnterScene(string clientId, ScenelData scenelData)
        {
            lock (_lock)
            {
                var nonNullData = scenelData ?? new ScenelData("", "");
                var player = _playerManager.GetPlayer(clientId);
                
                if (player == null)
                {
                    Console.WriteLine($"[SceneManager] ⚠️ 未找到玩家信息, ClientId={clientId}");
                    return false;
                }

                Console.WriteLine($"[SceneManager] {player.SteamName} 进入场景: {nonNullData.SceneName}/{nonNullData.SubSceneName}");
                
                // 更新玩家的场景数据
                _playerManager.UpdatePlayerSceneDataByClientId(clientId, nonNullData);
                
                return true;
            }
        }

        /// <summary>
        /// 玩家离开场景
        /// </summary>
        public bool LeaveScene(string clientId, ScenelData scenelData)
        {
            lock (_lock)
            {
                var player = _playerManager.GetPlayer(clientId);
                
                if (player == null)
                {
                    Console.WriteLine($"[SceneManager] ⚠️ 未找到玩家信息, ClientId={clientId}");
                    return false;
                }

                Console.WriteLine($"[SceneManager] {player.SteamName} 离开场景: {scenelData.SceneName}/{scenelData.SubSceneName}");
                
                // 清除玩家的场景数据
                _playerManager.UpdatePlayerSceneDataByClientId(clientId, new ScenelData("", ""));
                
                return true;
            }
        }

        /// <summary>
        /// 获取场景内的玩家列表
        /// </summary>
        public PlayerInfo[] GetScenePlayers(string clientId, ScenelData scenelData)
        {
            lock (_lock)
            {
                var playerInfo = _playerManager.GetPlayer(clientId);
                if (playerInfo == null)
                {
                    return Array.Empty<PlayerInfo>();
                }

                var room = _roomManager.GetPlayerRoom(playerInfo);
                if (room == null)
                {
                    return Array.Empty<PlayerInfo>();
                }

                var roomPlayers = _roomManager.GetRoomPlayers(room.RoomId);
                
                // 匹配相同场景和子场景的玩家
                var matchedPlayers = roomPlayers
                    .Where(p => p.CurrentScenelData.SceneName == scenelData.SceneName && 
                               p.CurrentScenelData.SubSceneName == scenelData.SubSceneName)
                    .ToArray();
                
                return matchedPlayers;
            }
        }

        /// <summary>
        /// 获取同房间同场景的其他玩家（不包括自己）
        /// </summary>
        public PlayerInfo[] GetOtherPlayersInSameScene(PlayerInfo player)
        {
            lock (_lock)
            {
                var room = _roomManager.GetPlayerRoom(player);
                if (room == null)
                {
                    return Array.Empty<PlayerInfo>();
                }

                var roomPlayers = _roomManager.GetRoomPlayers(room.RoomId);
                
                return roomPlayers
                    .Where(p => p.SteamId != player.SteamId && 
                               IsSameScene(player, p))
                    .ToArray();
            }
        }

        /// <summary>
        /// 检查两个玩家是否在同一场景（包括子场景）
        /// </summary>
        public bool IsSameScene(PlayerInfo player1, PlayerInfo player2)
        {
            if (player1.CurrentScenelData == null || player2.CurrentScenelData == null)
                return false;

            return player1.CurrentScenelData.SceneName == player2.CurrentScenelData.SceneName &&
                   player1.CurrentScenelData.SubSceneName == player2.CurrentScenelData.SubSceneName;
        }

        /// <summary>
        /// 检查玩家是否在指定场景中
        /// </summary>
        public bool IsPlayerInScene(PlayerInfo player, string sceneName, string subSceneName = "")
        {
            if (player.CurrentScenelData == null)
                return false;

            bool sceneMatch = player.CurrentScenelData.SceneName == sceneName;
            
            if (string.IsNullOrEmpty(subSceneName))
            {
                return sceneMatch;
            }
            
            return sceneMatch && player.CurrentScenelData.SubSceneName == subSceneName;
        }

        /// <summary>
        /// 更新玩家位置（从位置同步中调用）
        /// </summary>
        public void UpdatePlayerPosition(string steamId, float x, float y, float z)
        {
            lock (_lock)
            {
                _playerPositions[steamId] = new Vector3Data(x, y, z);
            }
        }

        /// <summary>
        /// 获取玩家位置
        /// </summary>
        public Vector3Data? GetPlayerPosition(string steamId)
        {
            lock (_lock)
            {
                return _playerPositions.TryGetValue(steamId, out var pos) ? pos : null;
            }
        }

        /// <summary>
        /// 获取场景内所有玩家的位置
        /// </summary>
        public Dictionary<string, Vector3Data> GetScenePlayerPositions(string sceneName, string subSceneName)
        {
            lock (_lock)
            {
                var result = new Dictionary<string, Vector3Data>();
                
                var scenePlayers = _playerManager.GetAllOnlinePlayers()
                    .Where(p => IsPlayerInScene(p, sceneName, subSceneName));

                foreach (var player in scenePlayers)
                {
                    if (_playerPositions.TryGetValue(player.SteamId, out var pos))
                    {
                        result[player.SteamId] = pos;
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// 移除玩家位置（断开连接时）
        /// </summary>
        public void RemovePlayerPosition(string steamId)
        {
            lock (_lock)
            {
                _playerPositions.Remove(steamId);
            }
        }
    }

    /// <summary>
    /// 简单的 3D 位置数据结构
    /// </summary>
    public struct Vector3Data
    {
        public float X, Y, Z;
        
        public Vector3Data(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}

