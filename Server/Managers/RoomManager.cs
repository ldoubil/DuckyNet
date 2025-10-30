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
        private readonly Dictionary<string, HashSet<PlayerInfo>> _roomPlayers = new Dictionary<string, HashSet<PlayerInfo>>();

        // 玩家所在房间：SteamId -> RoomId
        private readonly Dictionary<string, RoomInfo> _playerRoom = new Dictionary<string, RoomInfo>();

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
                    throw new InvalidOperationException("玩家已在其他房间");
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
                _roomPlayers[roomId] = new HashSet<PlayerInfo> { host };
                _playerRoom[host.SteamId] = room;

                Console.WriteLine($"[RoomManager] 房间创建: {roomId} 房主: {host.SteamName}");
                return room;
            }
        }

        /// <summary>
        /// 加入房间
        /// </summary>
        public RoomOperationResult JoinRoom(PlayerInfo player, JoinRoomRequest request)
        {
            lock (_lock)
            {
                // 检查玩家是否已在其他房间
                if (_playerRoom.ContainsKey(player.SteamId))
                {
                    return new RoomOperationResult
                    {
                        Success = false,
                        ErrorMessage = "玩家已在其他房间"
                    };
                }

                // 检查房间是否存在
                if (!_rooms.TryGetValue(request.RoomId, out var room))
                {
                    return new RoomOperationResult
                    {
                        Success = false,
                        ErrorMessage = "房间不存在"
                    };
                }

                // 检查是否是原房主回归（房主有特权，即使房间满了也能进）
                bool isReturningHost = room.HostSteamId == player.SteamId;

                // 检查房间是否已满（房主例外）
                if (room.IsFull && !isReturningHost)
                {
                    return new RoomOperationResult
                    {
                        Success = false,
                        ErrorMessage = "房间已满"
                    };
                }

                // 检查房间状态（房主例外）
                if (!room.CanJoin && !isReturningHost)
                {
                    return new RoomOperationResult
                    {
                        Success = false,
                        ErrorMessage = "房间不接受新玩家"
                    };
                }

                // 验证密码（房主回归免密码）
                if (room.RequirePassword && room.Password != request.Password && !isReturningHost)
                {
                    return new RoomOperationResult
                    {
                        Success = false,
                        ErrorMessage = "密码错误"
                    };
                }

                // 加入房间
                _roomPlayers[request.RoomId].Add(player);
                _playerRoom[player.SteamId] = room;
                room.CurrentPlayers++;

                if (isReturningHost)
                {
                    Console.WriteLine($"[RoomManager] 房主 {player.SteamName} 返回房间 {request.RoomId}");
                }
                else
                {
                    Console.WriteLine($"[RoomManager] 玩家 {player.SteamName} 加入房间 {request.RoomId}");
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
        public RoomInfo? LeaveRoom(PlayerInfo player)
        {
            lock (_lock)
            {
                if (!_playerRoom.TryGetValue(player.SteamId, out var room))
                {
                    return null; // 玩家不在任何房间
                }

                _roomPlayers[room.RoomId].Remove(player);
                _playerRoom.Remove(player.SteamId);
                room.CurrentPlayers--;

                Console.WriteLine($"[RoomManager] 玩家 {player.SteamName} 离开房间 {room.RoomId}");

                // 如果房间为空，删除房间
                if (room.CurrentPlayers == 0)
                {
                    _rooms.Remove(room.RoomId);
                    _roomPlayers.Remove(room.RoomId);
                    Console.WriteLine($"[RoomManager] 房间 {room.RoomId} 删除 (空)");
                    return null;
                }

                // 房主ID永远绑定房间，不转移房主权限
                // 房主可以离开并重新加入，保持房主身份
                if (room.HostSteamId == player.SteamId)
                {
                    Console.WriteLine($"[RoomManager] 房主 {player.SteamName} 离开房间 {room.RoomId}, 但房主ID保持不变 (等待房主返回)");
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
        public RoomInfo? GetPlayerRoom(PlayerInfo player)
        {
            lock (_lock)
            {
                if (_playerRoom.TryGetValue(player.SteamId, out var room))
                {
                    return room;
                }
                return null;
            }
        }

        /// <summary>
        /// 获取房间内的玩家ID列表
        /// </summary>
        public PlayerInfo[] GetRoomPlayers(string roomId)
        {
            lock (_lock)
            {
                return _roomPlayers.TryGetValue(roomId, out var players) ? players.ToArray() : Array.Empty<PlayerInfo>();
            }
        }

        /// <summary>
        /// 踢出玩家（仅房主可用）
        /// </summary>
        public bool KickPlayer(PlayerInfo host, PlayerInfo target)
        {
            lock (_lock)
            {
                // 检查房主是否在房间
                if (!_playerRoom.TryGetValue(host.SteamId, out var room))
                {
                    return false;
                }

                // 检查是否是房主
                if (room.HostSteamId != host.SteamId)
                {
                    return false;
                }

                // 检查目标玩家是否在同一房间
                if (!_playerRoom.TryGetValue(target.SteamId, out var targetRoom) || targetRoom.RoomId != room.RoomId)
                {
                    return false;
                }

                // 不能踢自己
                if (host.SteamId == target.SteamId)
                {
                    return false;
                }

                // 踢出玩家
                LeaveRoom(target);
                Console.WriteLine($"[RoomManager] 玩家 {target.SteamName} 被房主 {host.SteamName} 踢出房间 {room.RoomId}");
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

    }
}

