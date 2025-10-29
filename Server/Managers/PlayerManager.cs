using System;
using System.Collections.Generic;
using System.Linq;
using DuckyNet.Shared.Services;
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
        // 全局玩家表：ClientId -> PlayerInfo
        private readonly Dictionary<string, PlayerInfo> _allPlayers = new Dictionary<string, PlayerInfo>();

        // SteamId 到玩家映射：SteamId -> PlayerInfo
        private readonly Dictionary<string, PlayerInfo> _playersBySteamId = new Dictionary<string, PlayerInfo>();

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
        public void OnClientConnected(string clientId)
        {
            lock (_lock)
            {
                var pending = new PendingConnection
                {
                    ClientId = clientId,
                    ConnectTime = DateTime.UtcNow
                };
                _pendingConnections[clientId] = pending;

                Console.WriteLine($"[PlayerManager] Client connected: {clientId}, waiting for login (3s timeout)");
            }
        }

        /// <summary>
        /// 当客户端登录时调用
        /// </summary>
        public LoginResult OnClientLogin(string clientId, PlayerInfo playerInfo)
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

                // 检查是否有待登录连接
                if (!_pendingConnections.ContainsKey(clientId))
                {
                    // 可能已经登录过或超时
                    if (_allPlayers.ContainsKey(clientId))
                    {
                        return new LoginResult
                        {
                            Success = false,
                            ErrorMessage = "Already logged in"
                        };
                    }
                }

                // 从待登录列表移除
                _pendingConnections.Remove(clientId);

                _allPlayers[clientId] = playerInfo;
                _playersBySteamId[playerInfo.SteamId] = playerInfo;

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
        public void OnClientDisconnected(string clientId)
        {
            lock (_lock)
            {
                // 从待登录列表移除
                _pendingConnections.Remove(clientId);

                // 从玩家表移除
                if (_allPlayers.TryGetValue(clientId, out var player))
                {
                    _allPlayers.Remove(clientId);
                    _playersBySteamId.Remove(player.SteamId);
                    Console.WriteLine($"[PlayerManager] Player disconnected: {player.SteamName}");

                    // 从房间移除
                    _roomManager?.LeaveRoom(player.SteamId);
                }
            }
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
        public PlayerInfo? GetPlayer(string clientId)
        {
            lock (_lock)
            {
                return _allPlayers.TryGetValue(clientId, out var player) ? player : null;
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
        /// 检查玩家是否已登录
        /// </summary>
        public bool IsLoggedIn(string clientId)
        {
            lock (_lock)
            {
                return _allPlayers.ContainsKey(clientId);
            }
        }

        /// <summary>
        /// 获取全局在线玩家列表
        /// </summary>
        public PlayerInfo[] GetAllOnlinePlayers()
        {
            lock (_lock)
            {
                return _allPlayers.Values.ToArray();
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
                var room = _roomManager.GetPlayerRoom(clientId);
                if (room == null)
                {
                    return Array.Empty<PlayerInfo>();
                }

                var playerIds = _roomManager.GetRoomPlayerIds(room.RoomId);
                var players = new List<PlayerInfo>();

                foreach (var playerId in playerIds)
                {
                    if (_allPlayers.TryGetValue(playerId, out var player))
                    {
                        players.Add(player);
                    }
                }

                return players.ToArray();
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
                var playerIds = _roomManager.GetRoomPlayerIds(roomId);
                var players = new List<PlayerInfo>();

                foreach (var playerId in playerIds)
                {
                    // playerIds 现在是 SteamId 列表，使用 _playersBySteamId 查找
                    if (_playersBySteamId.TryGetValue(playerId, out var player))
                    {
                        players.Add(player);
                    }
                }

                return players.ToArray();
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
                    TotalPlayers: _allPlayers.Count,
                    PendingLogins: _pendingConnections.Count
                );
            }
        }
    }
}

