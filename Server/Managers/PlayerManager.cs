using System;
using System.Collections.Generic;
using System.Linq;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;
using DuckyNet.Server.RPC;

namespace DuckyNet.Server.Managers
{
    /// <summary>
    /// 待登录的连接
    /// </summary>
    public class PendingConnection
    {
        public string ClientId { get; set; } = string.Empty;
        public DateTime ConnectTime { get; set; }
        public const int LOGIN_TIMEOUT_SECONDS = 3;

        public bool IsTimedOut()
        {
            return (DateTime.UtcNow - ConnectTime).TotalSeconds > LOGIN_TIMEOUT_SECONDS;
        }
    }

    /// <summary>
    /// 玩家管理器
    /// 负责管理所有在线玩家和登录超时检查
    /// </summary>
    public class PlayerManager
    {

        // ClientId 到玩家映射：ClientId -> PlayerInfo
        private readonly Dictionary<string, PlayerInfo> _playersByClientId = new Dictionary<string, PlayerInfo>();
        // SteamId 到玩家映射：SteamId -> PlayerInfo
        private readonly Dictionary<string, PlayerInfo> _playersBySteamId = new Dictionary<string, PlayerInfo>();
        // SteamId 到 ClientId 的反向映射：SteamId -> ClientId（用于快速查询）
        private readonly Dictionary<string, string> _clientIdBySteamId = new Dictionary<string, string>();

        // 待登录连接表：ClientId -> PendingConnection
        private readonly Dictionary<string, PendingConnection> _pendingConnections = new Dictionary<string, PendingConnection>();

        private readonly object _lock = new object();
        private readonly RpcServer _server;
        private readonly RoomManager? _roomManager;

        public PlayerManager(RpcServer server, RoomManager? roomManager = null)
        {
            _server = server;
            _roomManager = roomManager;
        }

        /// <summary>
        /// 当客户端连接时调用
        /// </summary>
        public void OnClientConnected(string ClientId)
        {
            lock (_lock)
            {
                var pending = new PendingConnection
                {
                    ClientId = ClientId,        
                    ConnectTime = DateTime.UtcNow
                };
                _pendingConnections[ClientId] = pending;

                Console.WriteLine($"[PlayerManager] Client connected: {ClientId}, waiting for login (3s timeout)");
            }
        }

        /// <summary>
        /// 当客户端登录时调用
        /// </summary>
        public LoginResult OnClientLogin(string ClientId, PlayerInfo playerInfo)
        {
            lock (_lock)
            {
                // 验证玩家信息
                if (!playerInfo.IsValid())
                {
                    return new LoginResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid player info"
                    };
                }

                // 检查是否有待登录连接（按 ClientId）
                if (!_pendingConnections.ContainsKey(ClientId))
                {
                    // 可能已经登录过或超时
                    if (_playersBySteamId.ContainsKey(playerInfo.SteamId))
                    {
                        return new LoginResult
                        {
                            Success = false,
                            ErrorMessage = "Already logged in"
                        };
                    }
                }

                // 从待登录列表移除（按 ClientId）
                _pendingConnections.Remove(ClientId);

                // 建立三个映射（保持数据一致性）
                _playersByClientId[ClientId] = playerInfo;
                _playersBySteamId[playerInfo.SteamId] = playerInfo;
                _clientIdBySteamId[playerInfo.SteamId] = ClientId;

                Console.WriteLine($"[PlayerManager] Player logged in: {playerInfo.SteamName} ({playerInfo.SteamId})");

                return new LoginResult
                {
                    Success = true,
                    PlayerInfo = playerInfo,
                    SessionToken = Guid.NewGuid().ToString()
                };
            }
        }

        /// <summary>
        /// 当客户端断开连接时调用
        /// </summary>
        public void OnClientDisconnected(string ClientId)
        {
            lock (_lock)
            {
                // 先根据 ClientId 查找玩家
                if (_playersByClientId.TryGetValue(ClientId, out var player))
                {
                    // 从三个映射中移除（保持数据一致性）
                    _playersByClientId.Remove(ClientId);
                    _playersBySteamId.Remove(player.SteamId);
                    _clientIdBySteamId.Remove(player.SteamId);
                    Console.WriteLine($"[PlayerManager] Player disconnected: {player.SteamName}");

                    // 🔥 清理位置缓存（新增）
                    _sceneManager?.RemovePlayerPosition(player.SteamId);

                    // 从房间移除
                    _roomManager?.LeaveRoom(player);
                }
                else
                {
                    // 如果未登录过，尝试从待登录中移除（例如登录前断开）
                    _pendingConnections.Remove(ClientId);
                }
            }
        }

        /// <summary>
        /// 获取 SceneManager（用于位置缓存清理）
        /// </summary>
        private SceneManager? _sceneManager;
        
        public void SetSceneManager(SceneManager sceneManager)
        {
            _sceneManager = sceneManager;
        }

        /// <summary>
        /// 检查登录超时（应在主循环中定期调用）
        /// </summary>
        public void CheckLoginTimeouts()
        {
            List<string> timedOutClients;

            lock (_lock)
            {
                timedOutClients = _pendingConnections
                    .Where(kvp => kvp.Value.IsTimedOut())
                    .Select(kvp => kvp.Key)
                    .ToList();
            }

            // 断开超时连接
            foreach (var clientId in timedOutClients)
            {
                Console.WriteLine($"[PlayerManager] Login timeout: {clientId}, disconnecting...");
                
                lock (_lock)
                {
                    _pendingConnections.Remove(clientId);
                }

                // 断开连接
                _server.DisconnectClient(clientId, "Login timeout");
            }
        }

        /// <summary>
        /// 获取玩家信息（通过 ClientId）
        /// </summary>
        public PlayerInfo? GetPlayer(string ClientId)
        {
            lock (_lock)
            {
                return _playersByClientId.TryGetValue(ClientId, out var player) ? player : null;
            }
        }

        /// <summary>
        /// 通过 SteamId 获取玩家信息
        /// </summary>
        public PlayerInfo? GetPlayerBySteamId(string steamId)
        {
            lock (_lock)
            {
                return _playersBySteamId.TryGetValue(steamId, out var player) ? player : null;
            }
        }

        /// <summary>
        /// 通过 SteamId 获取对应的 ClientId（如果在线）
        /// </summary>
        /// <param name="steamId">玩家的 SteamId</param>
        /// <returns>对应的 ClientId；若未找到返回 null</returns>
        public string? GetClientIdBySteamId(string steamId)
        {
            lock (_lock)
            {
                // 使用字典索引实现 O(1) 查询（统一的高效方式）
                return _clientIdBySteamId.TryGetValue(steamId, out var clientId) ? clientId : null;
            }
        }

        /// <summary>
        /// 通过 ClientId 更新玩家的场景数据（SceneName/SubSceneName）
        /// </summary>
        /// <param name="clientId">客户端连接标识</param>
        /// <param name="scenelData">场景数据（允许 null，将被标准化为空场景）</param>
        /// <returns>是否更新成功（玩家存在时返回 true）</returns>
        public bool UpdatePlayerSceneDataByClientId(string clientId, ScenelData? scenelData)
        {
            lock (_lock)
            {
                if (_playersByClientId.TryGetValue(clientId, out var player))
                {
                    player.CurrentScenelData = scenelData ?? new ScenelData("", "");
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 通过 SteamId 更新玩家的场景数据（SceneName/SubSceneName）
        /// </summary>
        /// <param name="steamId">玩家 SteamId</param>
        /// <param name="scenelData">场景数据（允许 null，将被标准化为空场景）</param>
        /// <returns>是否更新成功（玩家存在时返回 true）</returns>
        public bool UpdatePlayerSceneDataBySteamId(string steamId, ScenelData? scenelData)
        {
            lock (_lock)
            {
                if (_playersBySteamId.TryGetValue(steamId, out var player))
                {
                    player.CurrentScenelData = scenelData ?? new ScenelData("", "");
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 检查玩家是否已登录
        /// </summary>
        public bool IsLoggedIn(string SteamId)
        {
            lock (_lock)
            {
                return _playersBySteamId.ContainsKey(SteamId);
            }
        }

        /// <summary>
        /// 获取全局在线玩家列表
        /// </summary>
        public PlayerInfo[] GetAllOnlinePlayers()
        {
            lock (_lock)
            {
                    return _playersBySteamId.Values.ToArray();
            }
        }

        /// <summary>
        /// 获取当前房间玩家列表
        /// </summary>
        public PlayerInfo[] GetCurrentRoomPlayers(string clientId)
        {
            if (_roomManager == null)
            {
                return Array.Empty<PlayerInfo>();
            }

            lock (_lock)
            {
                var player = GetPlayer(clientId);
                if (player == null)
                {
                    return Array.Empty<PlayerInfo>();
                }

                var room = _roomManager.GetPlayerRoom(player);
                return room != null ? _roomManager.GetRoomPlayers(room.RoomId) : Array.Empty<PlayerInfo>();
            }
        }

        /// <summary>
        /// 获取指定房间的玩家列表
        /// </summary>
        public PlayerInfo[] GetRoomPlayers(string roomId)
        {
            if (_roomManager == null)
            {
                return Array.Empty<PlayerInfo>();
            }

            lock (_lock)
            {
                var players = _roomManager.GetRoomPlayers(roomId);
                return players;
            }
        }


        /// <summary>
        /// 获取同场景的其他玩家（用于热区计算）
        /// </summary>
        public List<PlayerInfo> GetScenePlayers(PlayerInfo player, bool excludeSelf = true)
        {
            lock (_lock)
            {
                var scenePlayers = _playersBySteamId.Values
                    .Where(p => p.CurrentScenelData != null &&
                               p.CurrentScenelData.SceneName == player.CurrentScenelData?.SceneName &&
                               p.CurrentScenelData.SubSceneName == player.CurrentScenelData?.SubSceneName)
                    .ToList();

                if (excludeSelf)
                {
                    scenePlayers = scenePlayers.Where(p => p.SteamId != player.SteamId).ToList();
                }

                return scenePlayers;
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public (int TotalPlayers, int PendingLogins) GetStatistics()
        {
            lock (_lock)
            {
                return (
                    TotalPlayers: _playersBySteamId.Count,
                    PendingLogins: _pendingConnections.Count
                );
            }
        }
    }
}

