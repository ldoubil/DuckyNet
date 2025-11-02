using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Managers;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 房间服务实现
    /// </summary>
    public class RoomServiceImpl : IRoomService
    {
        private readonly RpcServer _server;
        private readonly RoomManager _roomManager;
        private readonly PlayerManager _playerManager;
        private readonly PlayerUnitySyncServiceImpl _unitySyncService;

        public RoomServiceImpl(RpcServer server, RoomManager roomManager, PlayerManager playerManager, PlayerUnitySyncServiceImpl unitySyncService)
        {
            _server = server;
            _roomManager = roomManager;
            _playerManager = playerManager;
            _unitySyncService = unitySyncService;
        }

        public async Task<RoomInfo[]> GetRoomListAsync(IClientContext client)
        {
            // 检查是否已登录
            if (!_playerManager.IsLoggedIn(_playerManager.GetPlayer(client.ClientId)?.SteamId ?? ""))
            {
                throw new UnauthorizedAccessException("Not logged in");
            }

            var rooms = _roomManager.GetAllRooms();
            return await Task.FromResult(rooms);
        }

        public async Task<RoomOperationResult> CreateRoomAsync(IClientContext client, CreateRoomRequest request)
        {
            // 检查是否已登录
            if (!_playerManager.IsLoggedIn(_playerManager.GetPlayer(client.ClientId)?.SteamId ?? ""))
            {
                return new RoomOperationResult
                {
                    Success = false,
                    ErrorMessage = "Not logged in"
                };
            }

            var player = _playerManager.GetPlayer(client.ClientId);
            if (player == null)
            {
                return new RoomOperationResult
                {
                    Success = false,
                    ErrorMessage = "Player not found"
                };
            }

            try
            {
                var room = _roomManager.CreateRoom(player, request);
                
                Console.WriteLine($"[RoomService] Room created: {room.RoomId} by {player.SteamName}");
                Console.WriteLine($"[RoomService] 房主当前场景: '{player.CurrentScenelData.SceneName}' (子场景: '{player.CurrentScenelData.SubSceneName}')");

                // 🔥 虽然房间里只有房主，但我们不需要通知房主"自己加入了"
                // 客户端会通过 RoomOperationResult.Room 知道自己在房间中
                // 当其他玩家加入时，JoinRoomAsync 会正确处理双向通知

                return await Task.FromResult(new RoomOperationResult
                {
                    Success = true,
                    Room = room
                });
            }
            catch (InvalidOperationException ex)
            {
                return new RoomOperationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<RoomOperationResult> JoinRoomAsync(IClientContext client, JoinRoomRequest request)
        {
            // 检查是否已登录
            if (!_playerManager.IsLoggedIn(_playerManager.GetPlayer(client.ClientId)?.SteamId ?? ""))
            {
                return new RoomOperationResult
                {
                    Success = false,
                    ErrorMessage = "Not logged in"
                };
            }

            var player = _playerManager.GetPlayer(client.ClientId);
            if (player == null)
            {
                return new RoomOperationResult
                {
                    Success = false,
                    ErrorMessage = "Player not found"
                };
            }

            // 🔥 记录玩家加入时的场景状态
            Console.WriteLine($"[RoomService] 玩家 {player.SteamName} 加入房间，当前场景: '{player.CurrentScenelData.SceneName}' (子场景: '{player.CurrentScenelData.SubSceneName}')");

            var result = _roomManager.JoinRoom(player, request);

            if (result.Success && result.Room != null)
            {
                // 获取房间内所有玩家（包括新加入的）
                var roomPlayers = _roomManager.GetRoomPlayers(request.RoomId);
                
                // 1. 通知新玩家：房间内已有的其他玩家（让新玩家创建其他人的角色）
                var otherPlayers = roomPlayers.Where(p => p.SteamId != player.SteamId).ToList();
                if (otherPlayers.Any())
                {
                    var newPlayerContext = _server.GetClientContext(player.SteamId);
                    if (newPlayerContext != null)
                    {
                        foreach (var existingPlayer in otherPlayers)
                        {
                            // 通知新玩家有这个玩家
                            newPlayerContext.Call<IRoomClientService>()
                                .OnPlayerJoinedRoom(existingPlayer, result.Room);
                            Console.WriteLine($"[RoomService] 通知新玩家 {player.SteamName}: 房间内已有玩家 {existingPlayer.SteamName}");
                            // 🔥 如果现有玩家在场景中,通知新玩家场景信息
                            if (!string.IsNullOrEmpty(existingPlayer.CurrentScenelData.SceneName))
                            {
                                newPlayerContext.Call<ISceneClientService>()
                                    .OnPlayerEnteredScene(existingPlayer, existingPlayer.CurrentScenelData);
                                Console.WriteLine($"[RoomService] ✅ 通知新玩家 {player.SteamName}: {existingPlayer.SteamName} 在场景 {existingPlayer.CurrentScenelData.SceneName}");
                            }
                            else
                            {
                                Console.WriteLine($"[RoomService] ⚠️ 现有玩家 {existingPlayer.SteamName} 场景信息为空，跳过场景通知");
                            }
                            // 🔥 如果现有玩家在场景中,发送位置数据
                            var lastPosition = _unitySyncService.GetLastPosition(existingPlayer.SteamId);
                            if (lastPosition != null)
                            {
                                newPlayerContext.Call<IPlayerClientService>()
                                    .OnPlayerUnitySyncReceived(lastPosition);
                                Console.WriteLine($"[RoomService] 发送 {existingPlayer.SteamName} 的最后位置给新玩家 {player.SteamName}");
                            }
                            else
                            {
                                // 🔥 关键修复：如果缓存为空，创建一个默认位置（Vector3.zero）
                                // 这样新玩家至少能创建角色，等下次位置同步时再更新位置
                                Console.WriteLine($"[RoomService] ⚠️ 现有玩家 {existingPlayer.SteamName} 无位置缓存，发送默认位置");
                                var defaultSyncData = new UnitySyncData
                                {
                                    SteamId = existingPlayer.SteamId,
                                    SequenceNumber = 0
                                };
                                defaultSyncData.SetPosition(0, 0, 0);
                                defaultSyncData.SetRotation(0, 0, 0, 1);
                                defaultSyncData.SetVelocity(0, 0, 0);
                                
                                newPlayerContext.Call<IPlayerClientService>()
                                    .OnPlayerUnitySyncReceived(defaultSyncData);
                                Console.WriteLine($"[RoomService] ✅ 已发送默认位置给新玩家 {player.SteamName}");
                            }
                            
                            
                        }
                    }
                }
                
                // 2. 通知房间内所有其他玩家：新玩家加入了（让其他人创建新玩家的角色）
                foreach (var p in roomPlayers)
                {
                    if (p.SteamId == player.SteamId) continue; // 跳过自己
                    
                    var playerContext = _server.GetClientContext(p.SteamId);
                    if (playerContext != null)
                    {
                        playerContext.Call<IRoomClientService>()
                            .OnPlayerJoinedRoom(player, result.Room);
                        Console.WriteLine($"[RoomService] 通知玩家 {p.SteamName}: 新玩家 {player.SteamName} 加入了");
                        
                        // 🔥 如果新玩家已经在场景中,通知其他玩家
                        if (!string.IsNullOrEmpty(player.CurrentScenelData.SceneName))
                        {
                            playerContext.Call<ISceneClientService>()
                                .OnPlayerEnteredScene(player, player.CurrentScenelData);
                            Console.WriteLine($"[RoomService] ✅ 通知玩家 {p.SteamName}: 新玩家 {player.SteamName} 已在场景 {player.CurrentScenelData.SceneName}");
                        }
                        else
                        {
                            Console.WriteLine($"[RoomService] ⚠️ 新玩家 {player.SteamName} 场景信息为空，跳过场景通知");
                        }
                    }
                }

                Console.WriteLine($"[RoomService] Player {player.SteamName} joined room {request.RoomId}");
            }

            return await Task.FromResult(result);
        }

        public async Task<bool> LeaveRoomAsync(IClientContext client)
        {
            if (!_playerManager.IsLoggedIn(_playerManager.GetPlayer(client.ClientId)?.SteamId ?? ""))
            {
                return false;
            }

            var player = _playerManager.GetPlayer(client.ClientId);
            if (player == null)
            {
                return false;
            }

            var room = _roomManager.LeaveRoom(player);

            if (room != null)
            {
                // 🔥 清除玩家的位置缓存
                _unitySyncService.ClearPlayerPosition(player.SteamId);
                
                // 通知房间内其他玩家
                var roomPlayers = _roomManager.GetRoomPlayers(room.RoomId);
                foreach (var p in roomPlayers)
                {
                    var playerContext = _server.GetClientContext(p.SteamId);
                    if (playerContext != null)
                        playerContext.Call<IRoomClientService>()
                            .OnPlayerLeftRoom(player, room);
                    }

                Console.WriteLine($"[RoomService] Player {player.SteamName} left room {room.RoomId}");
            }

            return await Task.FromResult(true);
        }

        public async Task<RoomInfo?> GetCurrentRoomAsync(IClientContext client)
        {
            if (!_playerManager.IsLoggedIn(_playerManager.GetPlayer(client.ClientId)?.SteamId ?? ""))
            {
                return null;
            }

            var player = _playerManager.GetPlayer(client.ClientId);
            if (player == null)
            {
                return null;
            }

            var room = _roomManager.GetPlayerRoom(player);
            return await Task.FromResult(room);
        }

        public async Task<RoomInfo?> GetRoomInfoAsync(IClientContext client, string roomId)
        {
            if (!_playerManager.IsLoggedIn(client.ClientId))
            {
                return null;
            }

            var room = _roomManager.GetRoom(roomId);
            return await Task.FromResult(room);
        }

        public async Task<PlayerInfo[]> GetRoomPlayersAsync(IClientContext client, string roomId)
        {
            if (!_playerManager.IsLoggedIn(_playerManager.GetPlayer(client.ClientId)?.SteamId ?? ""))
            {
                return Array.Empty<PlayerInfo>();
            }

            var requester = _playerManager.GetPlayer(client.ClientId);
            if (requester == null)
            {
                return Array.Empty<PlayerInfo>();
            }

            var players = _playerManager.GetRoomPlayers(roomId);
            Console.WriteLine($"[RoomService] GetRoomPlayers 被调用: requester={requester.SteamName}, roomId={roomId}, players.Length={players.Length}");
            
            // 🔥 关键修复：主动向请求者发送房间内其他玩家的加入通知
            // 这样客户端的 PlayerManager 会创建 RemotePlayer
            var room = _roomManager.GetPlayerRoom(requester);
            Console.WriteLine($"[RoomService] GetRoomPlayers: room={(room != null ? room.RoomId : "null")}");
            if (room != null)
            {
                // 🔥 修复：使用 ClientId 而不是 SteamId
                var requesterClientId = _playerManager.GetClientIdBySteamId(requester.SteamId);
                if (!string.IsNullOrEmpty(requesterClientId))
                {
                    var requesterContext = _server.GetClientContext(requesterClientId);
                    if (requesterContext != null)
                    {
                        foreach (var otherPlayer in players)
                        {
                            // 跳过请求者自己
                            if (otherPlayer.SteamId == requester.SteamId) continue;

                            // 发送 OnPlayerJoinedRoom 通知
                            requesterContext.Call<IRoomClientService>()
                                .OnPlayerJoinedRoom(otherPlayer, room);
                            Console.WriteLine($"[RoomService] GetRoomPlayers: 通知 {requester.SteamName} 房间内有玩家 {otherPlayer.SteamName}");

                            // 如果对方在场景中，也通知场景信息
                            if (!string.IsNullOrEmpty(otherPlayer.CurrentScenelData.SceneName))
                            {
                                requesterContext.Call<ISceneClientService>()
                                    .OnPlayerEnteredScene(otherPlayer, otherPlayer.CurrentScenelData);
                                Console.WriteLine($"[RoomService] GetRoomPlayers: 通知 {requester.SteamName} 玩家 {otherPlayer.SteamName} 在场景 {otherPlayer.CurrentScenelData.SceneName}");
                                
                                // 🔥 关键修复：同时发送位置数据
                                var lastPosition = _unitySyncService.GetLastPosition(otherPlayer.SteamId);
                                if (lastPosition != null)
                                {
                                    requesterContext.Call<IPlayerClientService>()
                                        .OnPlayerUnitySyncReceived(lastPosition);
                                    Console.WriteLine($"[RoomService] GetRoomPlayers: 发送 {otherPlayer.SteamName} 的最后位置给 {requester.SteamName}");
                                }
                                else
                                {
                                    // 如果缓存为空，创建默认位置
                                    Console.WriteLine($"[RoomService] GetRoomPlayers: 现有玩家 {otherPlayer.SteamName} 无位置缓存，发送默认位置");
                                    var defaultSyncData = new UnitySyncData
                                    {
                                        SteamId = otherPlayer.SteamId,
                                        SequenceNumber = 0
                                    };
                                    defaultSyncData.SetPosition(0, 0, 0);
                                    defaultSyncData.SetRotation(0, 0, 0, 1);
                                    defaultSyncData.SetVelocity(0, 0, 0);
                                    
                                    requesterContext.Call<IPlayerClientService>()
                                        .OnPlayerUnitySyncReceived(defaultSyncData);
                                    Console.WriteLine($"[RoomService] GetRoomPlayers: ✅ 已发送默认位置给 {requester.SteamName}");
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[RoomService] ⚠️ GetRoomPlayers: 未找到客户端上下文 ClientId={requesterClientId}");
                    }
                }
                else
                {
                    Console.WriteLine($"[RoomService] ⚠️ GetRoomPlayers: 未找到ClientId SteamId={requester.SteamId}");
                }
            }

            return await Task.FromResult(players);
        }

        public async Task<bool> KickPlayerAsync(IClientContext client, string playerId)
        {
            if (!_playerManager.IsLoggedIn(_playerManager.GetPlayer(client.ClientId)?.SteamId ?? ""))
            {
                return false;
            }

            var player = _playerManager.GetPlayer(client.ClientId);
            if (player == null)
            {
                return false;
            }

            var targetPlayer = _playerManager.GetPlayer(playerId);
            if (targetPlayer == null)
            {
                return false;
            }

            var result = _roomManager.KickPlayer(player, targetPlayer);

            if (result)
            {
                // 通知被踢的玩家
                var targetContext = _server.GetClientContext(playerId);
                if (targetContext != null)
                {
                    targetContext.Call<IRoomClientService>()
                        .OnKickedFromRoom($"被房主 {player.SteamName} 踢出房间");
                }

                Console.WriteLine($"[RoomService] Player {playerId} kicked by {client.ClientId}");
            }

            return await Task.FromResult(result);
        }

    }
}

