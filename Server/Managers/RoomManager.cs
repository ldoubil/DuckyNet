using System;
using System.Collections.Generic;
using System.Linq;
using DuckyNet.Shared.Services;

namespace DuckyNet.Server.Managers
{
    /// <summary>
    /// 房间管理器
    /// 负责管理所有房间和房间内的玩家
    /// </summary>
    public class RoomManager
    {
        // 全局房间表：RoomId -> RoomInfo
        private readonly Dictionary<string, RoomInfo> _rooms = new Dictionary<string, RoomInfo>();

        // 房间内的玩家：RoomId -> HashSet<PlayerId>
        private readonly Dictionary<string, HashSet<string>> _roomPlayers = new Dictionary<string, HashSet<string>>();

        // 玩家所在房间：SteamId -> RoomId
        private readonly Dictionary<string, string> _playerRoom = new Dictionary<string, string>();

        private readonly object _lock = new object();
        private int _roomIdCounter = 1;

        /// <summary>
        /// 创建房间
        /// </summary>
        public RoomInfo CreateRoom(PlayerInfo host, CreateRoomRequest request)
        {
            lock (_lock)
            {
                // 检查玩家是否已在其他房间
                if (_playerRoom.ContainsKey(host.SteamId))
                {
                    throw new InvalidOperationException("Player is already in a room");
                }

                // 创建房间
                var roomId = GenerateRoomId();
                var room = new RoomInfo
                {
                    RoomId = roomId,
                    RoomName = request.RoomName,
                    Description = request.Description,
                    Password = request.Password,
                    HostSteamId = host.SteamId,
                    CurrentPlayers = 1,
                    MaxPlayers = request.MaxPlayers,
                    CreateTime = DateTime.UtcNow
                };

                _rooms[roomId] = room;
                _roomPlayers[roomId] = new HashSet<string> { host.SteamId };
                _playerRoom[host.SteamId] = roomId;

                Console.WriteLine($"[RoomManager] Room created: {roomId} by {host.SteamName}");
                return room;
            }
        }

        /// <summary>
        /// 加入房间
        /// </summary>
        public RoomOperationResult JoinRoom(string playerSteamId, string playerName, JoinRoomRequest request)
        {
            lock (_lock)
            {
                // 检查玩家是否已在其他房间
                if (_playerRoom.ContainsKey(playerSteamId))
                {
                    return new RoomOperationResult
                    {
                        Success = false,
                        ErrorMessage = "You are already in a room"
                    };
                }

                // 检查房间是否存在
                if (!_rooms.TryGetValue(request.RoomId, out var room))
                {
                    return new RoomOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Room not found"
                    };
                }

                // 检查是否是原房主回归（房主有特权，即使房间满了也能进）
                bool isReturningHost = (room.HostSteamId == playerSteamId);

                // 检查房间是否已满（房主例外）
                if (room.IsFull && !isReturningHost)
                {
                    return new RoomOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Room is full"
                    };
                }

                // 检查房间状态（房主例外）
                if (!room.CanJoin && !isReturningHost)
                {
                    return new RoomOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Room is not accepting new players"
                    };
                }

                // 验证密码（房主回归免密码）
                if (room.RequirePassword && room.Password != request.Password && !isReturningHost)
                {
                    return new RoomOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid password"
                    };
                }

                // 加入房间
                _roomPlayers[request.RoomId].Add(playerSteamId);
                _playerRoom[playerSteamId] = request.RoomId;
                room.CurrentPlayers++;

                if (isReturningHost)
                {
                    Console.WriteLine($"[RoomManager] Host {playerName} returned to room {request.RoomId}");
                }
                else
                {
                    Console.WriteLine($"[RoomManager] Player {playerName} joined room {request.RoomId}");
                }

                return new RoomOperationResult
                {
                    Success = true,
                    Room = room
                };
            }
        }

        /// <summary>
        /// 离开房间
        /// </summary>
        public RoomInfo? LeaveRoom(string playerId)
        {
            lock (_lock)
            {
                if (!_playerRoom.TryGetValue(playerId, out var roomId))
                {
                    return null; // 玩家不在任何房间
                }

                var room = _rooms[roomId];
                _roomPlayers[roomId].Remove(playerId);
                _playerRoom.Remove(playerId);
                room.CurrentPlayers--;

                Console.WriteLine($"[RoomManager] Player {playerId} left room {roomId}");

                // 如果房间为空，删除房间
                if (room.CurrentPlayers == 0)
                {
                    _rooms.Remove(roomId);
                    _roomPlayers.Remove(roomId);
                    Console.WriteLine($"[RoomManager] Room {roomId} deleted (empty)");
                    return null;
                }

                // 房主ID永远绑定房间，不转移房主权限
                // 房主可以离开并重新加入，保持房主身份
                if (room.HostSteamId == playerId)
                {
                    Console.WriteLine($"[RoomManager] Host left room {roomId}, but host ID remains (waiting for host to return)");
                }

                return room;
            }
        }

        /// <summary>
        /// 获取所有房间列表
        /// </summary>
        public RoomInfo[] GetAllRooms()
        {
            lock (_lock)
            {
                return _rooms.Values.ToArray();
            }
        }

        /// <summary>
        /// 获取房间信息
        /// </summary>
        public RoomInfo? GetRoom(string roomId)
        {
            lock (_lock)
            {
                return _rooms.TryGetValue(roomId, out var room) ? room : null;
            }
        }

        /// <summary>
        /// 获取玩家所在房间
        /// </summary>
        public RoomInfo? GetPlayerRoom(string playerId)
        {
            lock (_lock)
            {
                if (_playerRoom.TryGetValue(playerId, out var roomId))
                {
                    return _rooms.TryGetValue(roomId, out var room) ? room : null;
                }
                return null;
            }
        }

        /// <summary>
        /// 获取房间内的玩家ID列表
        /// </summary>
        public string[] GetRoomPlayerIds(string roomId)
        {
            lock (_lock)
            {
                if (_roomPlayers.TryGetValue(roomId, out var players))
                {
                    return players.ToArray();
                }
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// 踢出玩家（仅房主可用）
        /// </summary>
        public bool KickPlayer(string hostSteamId, string targetSteamId)
        {
            lock (_lock)
            {
                // 检查房主是否在房间
                if (!_playerRoom.TryGetValue(hostSteamId, out var roomId))
                {
                    return false;
                }

                var room = _rooms[roomId];

                // 检查是否是房主
                if (room.HostSteamId != hostSteamId)
                {
                    return false;
                }

                // 检查目标玩家是否在同一房间
                if (!_playerRoom.TryGetValue(targetSteamId, out var targetRoomId) || targetRoomId != roomId)
                {
                    return false;
                }

                // 不能踢自己
                if (hostSteamId == targetSteamId)
                {
                    return false;
                }

                // 踢出玩家
                LeaveRoom(targetSteamId);
                Console.WriteLine($"[RoomManager] Player {targetSteamId} kicked by host {hostSteamId}");
                return true;
            }
        }


        /// <summary>
        /// 生成房间ID
        /// </summary>
        private string GenerateRoomId()
        {
            return $"room-{_roomIdCounter++:D6}";
        }

        /// <summary>
        /// 获取房间统计信息
        /// </summary>
        public (int TotalRooms, int TotalPlayers) GetStatistics()
        {
            lock (_lock)
            {
                return (
                    TotalRooms: _rooms.Count,
                    TotalPlayers: _playerRoom.Count
                );
            }
        }
    }
}

