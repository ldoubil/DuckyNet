using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Server.Core;
using DuckyNet.Server.Events;
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
        private readonly PlayerUnitySyncServiceImpl _unitySyncService;
        private readonly EquipmentServerServiceImpl _equipmentService;
        private readonly WeaponSyncServerServiceImpl _weaponSyncService;

        public RoomServiceImpl(
            PlayerUnitySyncServiceImpl unitySyncService,
            EquipmentServerServiceImpl equipmentService,
            WeaponSyncServerServiceImpl weaponSyncService)
        {
            _unitySyncService = unitySyncService;
            _equipmentService = equipmentService;
            _weaponSyncService = weaponSyncService;
        }

        /// <summary>
        /// 验证玩家登录状态
        /// </summary>
        private PlayerInfo? ValidatePlayer(IClientContext client)
        {
            var player = ServerContext.Players.GetPlayer(client.ClientId);
            if (player == null || !ServerContext.Players.IsLoggedIn(player.SteamId))
            {
                return null;
            }
            return player;
        }

        public async Task<RoomInfo[]> GetRoomListAsync(IClientContext client)
        {
            if (ValidatePlayer(client) == null)
            {
                throw new UnauthorizedAccessException("Not logged in");
            }

            var rooms = ServerContext.Rooms.GetAllRooms();
            return await Task.FromResult(rooms);
        }

        public async Task<RoomOperationResult> CreateRoomAsync(IClientContext client, CreateRoomRequest request)
        {
            var player = ValidatePlayer(client);
            if (player == null)
            {
                return new RoomOperationResult
                {
                    Success = false,
                    ErrorMessage = "Not logged in"
                };
            }

            try
            {
                var room = ServerContext.Rooms.CreateRoom(player, request);
                
                Console.WriteLine($"[RoomService] Room created: {room.RoomId} by {player.SteamName}");

                // 发布房间创建事件
                ServerEventPublisher.PublishRoomCreated(room, player);

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
            var player = ValidatePlayer(client);
            if (player == null)
            {
                return new RoomOperationResult
                {
                    Success = false,
                    ErrorMessage = "Not logged in"
                };
            }

            Console.WriteLine($"[RoomService] 玩家 {player.SteamName} 加入房间");

            var result = ServerContext.Rooms.JoinRoom(player, request);

            if (result.Success && result.Room != null)
            {
                // 获取房间内所有玩家（包括新加入的）
                var roomPlayers = ServerContext.Players.GetRoomPlayers(request.RoomId);
                
                // 1. 通知新玩家：房间内已有的其他玩家
                var otherPlayers = roomPlayers.Where(p => p.SteamId != player.SteamId).ToList();
                if (otherPlayers.Any())
                {
                    foreach (var existingPlayer in otherPlayers)
                    {
                        // 通知房间成员关系
                        client.Call<IRoomClientService>()
                            .OnPlayerJoinedRoom(existingPlayer, result.Room);
                        
                        // 如果现有玩家在场景中，发送场景进入事件
                        if (existingPlayer.CurrentScenelData != null && !string.IsNullOrEmpty(existingPlayer.CurrentScenelData.SceneName))
                        {
                            client.Call<ISceneClientService>()
                                .OnPlayerEnteredScene(existingPlayer, existingPlayer.CurrentScenelData);
                            
                            // 发送位置数据
                            var lastPosition = _unitySyncService.GetLastPosition(existingPlayer.SteamId);
                            if (lastPosition != null)
                            {
                                client.Call<IPlayerClientService>()
                                    .OnPlayerUnitySyncReceived(lastPosition);
                            }
                        }
                    }
                    
                    // 发送装备和武器数据给新玩家
                    _equipmentService.SendAllEquipmentDataToPlayer(client.ClientId, request.RoomId);
                    _weaponSyncService.SendAllWeaponDataToPlayer(client.ClientId, request.RoomId);
                }
                
                // 2. 通知房间内所有其他玩家：新玩家加入了
                ServerContext.Broadcast.BroadcastToRoomExcludeSelf(player, (target, targetContext) =>
                {
                    // 通知房间成员关系
                    targetContext.Call<IRoomClientService>()
                        .OnPlayerJoinedRoom(player, result.Room);
                    
                    // 如果新玩家已经在场景中，发送场景进入事件和位置
                    if (!string.IsNullOrEmpty(player.CurrentScenelData.SceneName))
                    {
                        targetContext.Call<ISceneClientService>()
                            .OnPlayerEnteredScene(player, player.CurrentScenelData);
                        
                        var newPlayerLastPos = _unitySyncService.GetLastPosition(player.SteamId);
                        if (newPlayerLastPos != null)
                        {
                            targetContext.Call<IPlayerClientService>()
                                .OnPlayerUnitySyncReceived(newPlayerLastPos);
                        }
                    }
                });

                Console.WriteLine($"[RoomService] Player {player.SteamName} joined room {request.RoomId}");
                
                // 发布玩家加入房间事件
                ServerEventPublisher.PublishPlayerJoinedRoom(result.Room, player);
            }

            return await Task.FromResult(result);
        }

        public async Task<bool> LeaveRoomAsync(IClientContext client)
        {
            var player = ValidatePlayer(client);
            if (player == null)
            {
                return false;
            }

            var room = ServerContext.Rooms.LeaveRoom(player);

            if (room != null)
            {
                // 清除玩家的位置缓存和场景数据
                _unitySyncService.ClearPlayerPosition(player.SteamId);
                ServerContext.Players.UpdatePlayerSceneDataByClientId(client.ClientId, new ScenelData("", ""));
                Console.WriteLine($"[RoomService] {player.SteamName} 离开房间 {room.RoomId}");
                
                // 发布玩家离开房间事件
                ServerEventPublisher.PublishPlayerLeftRoom(room, player);
                
                // 通知房间内其他玩家
                ServerContext.Broadcast.BroadcastToRoomById(room.RoomId, (target, targetContext) =>
                {
                    targetContext.Call<IRoomClientService>()
                        .OnPlayerLeftRoom(player, room);
                });

                Console.WriteLine($"[RoomService] 已通知其他玩家 {player.SteamName} 离开");
            }

            return await Task.FromResult(true);
        }

        public async Task<RoomInfo?> GetCurrentRoomAsync(IClientContext client)
        {
            var player = ValidatePlayer(client);
            if (player == null)
            {
                return null;
            }

            var room = ServerContext.Rooms.GetPlayerRoom(player);
            return await Task.FromResult(room);
        }

        public async Task<RoomInfo?> GetRoomInfoAsync(IClientContext client, string roomId)
        {
            if (ValidatePlayer(client) == null)
            {
                return null;
            }

            var room = ServerContext.Rooms.GetRoom(roomId);
            return await Task.FromResult(room);
        }

        public async Task<PlayerInfo[]> GetRoomPlayersAsync(IClientContext client, string roomId)
        {
            var requester = ValidatePlayer(client);
            if (requester == null)
            {
                return Array.Empty<PlayerInfo>();
            }

            var players = ServerContext.Players.GetRoomPlayers(roomId);
            Console.WriteLine($"[RoomService] GetRoomPlayers: {requester.SteamName} 查询房间 {roomId}, 玩家数: {players.Length}");
            
            // 主动向请求者发送房间内其他玩家的加入通知
            var room = ServerContext.Rooms.GetPlayerRoom(requester);
            if (room != null)
            {
                foreach (var otherPlayer in players)
                {
                    if (otherPlayer.SteamId == requester.SteamId)
                        continue;

                    // 发送房间成员通知
                    client.Call<IRoomClientService>()
                        .OnPlayerJoinedRoom(otherPlayer, room);

                    // 如果对方在场景中，发送位置
                    if (!string.IsNullOrEmpty(otherPlayer.CurrentScenelData.SceneName))
                    {
                        var lastPosition = _unitySyncService.GetLastPosition(otherPlayer.SteamId);
                        if (lastPosition != null)
                        {
                            client.Call<IPlayerClientService>()
                                .OnPlayerUnitySyncReceived(lastPosition);
                        }
                    }
                }
            }

            return await Task.FromResult(players);
        }

        public async Task<bool> KickPlayerAsync(IClientContext client, string playerId)
        {
            var player = ValidatePlayer(client);
            if (player == null)
            {
                return false;
            }

            var targetPlayer = ServerContext.Players.GetPlayerBySteamId(playerId);
            if (targetPlayer == null)
            {
                return false;
            }

            var result = ServerContext.Rooms.KickPlayer(player, targetPlayer);

            if (result)
            {
                // 通知被踢的玩家
                ServerContext.Broadcast.SendToPlayer(targetPlayer.SteamId, context =>
                {
                    context.Call<IRoomClientService>()
                        .OnKickedFromRoom($"被房主 {player.SteamName} 踢出房间");
                });

                Console.WriteLine($"[RoomService] Player {targetPlayer.SteamName} kicked by {player.SteamName}");
            }

            return await Task.FromResult(result);
        }
    }
}
