using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Managers;
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
        private readonly RpcServer _server;
        private readonly RoomManager _roomManager;
        private readonly PlayerManager _playerManager;
        private readonly PlayerUnitySyncServiceImpl _unitySyncService;
        private EquipmentServerServiceImpl? _equipmentService; // 装备服务（延迟注入）
        private WeaponSyncServerServiceImpl? _weaponSyncService; // 武器服务（延迟注入）

        public RoomServiceImpl(RpcServer server, RoomManager roomManager, PlayerManager playerManager, PlayerUnitySyncServiceImpl unitySyncService)
        {
            _server = server;
            _roomManager = roomManager;
            _playerManager = playerManager;
            _unitySyncService = unitySyncService;
        }

        /// <summary>
        /// 设置装备服务（延迟注入，因为循环依赖）
        /// </summary>
        public void SetEquipmentService(EquipmentServerServiceImpl equipmentService)
        {
            _equipmentService = equipmentService;
        }

        /// <summary>
        /// 设置武器服务（延迟注入，因为循环依赖）
        /// </summary>
        public void SetWeaponSyncService(WeaponSyncServerServiceImpl weaponSyncService)
        {
            _weaponSyncService = weaponSyncService;
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

                // 发布房间创建事件
                ServerEventPublisher.PublishRoomCreated(room, player);

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
                    // 🔥 修复：使用 client.ClientId 而不是 SteamId
                    var newPlayerContext = _server.GetClientContext(client.ClientId);
                    if (newPlayerContext != null)
                    {
                        foreach (var existingPlayer in otherPlayers)
                        {
                            // 🔥 简化：只通知房间成员关系
                            newPlayerContext.Call<IRoomClientService>()
                                .OnPlayerJoinedRoom(existingPlayer, result.Room);
                            Console.WriteLine($"[RoomService] ✅ 通知新玩家 {player.SteamName}: 房间内已有玩家 {existingPlayer.SteamName}");
                            Console.WriteLine($"[RoomService] ✅ 玩家 {existingPlayer.SteamName} 的头像URL: {existingPlayer.AvatarUrl ?? "(null)"}");
                            
                            // 🔥 如果现有玩家在场景中，发送场景进入事件
                            Console.WriteLine($"[RoomService] 检查玩家 {existingPlayer.SteamName} 场景数据: SceneName='{existingPlayer.CurrentScenelData?.SceneName}', SubSceneName='{existingPlayer.CurrentScenelData?.SubSceneName}'");
                            
                            if (existingPlayer.CurrentScenelData != null && !string.IsNullOrEmpty(existingPlayer.CurrentScenelData.SceneName))
                            {
                                newPlayerContext.Call<ISceneClientService>()
                                    .OnPlayerEnteredScene(existingPlayer, existingPlayer.CurrentScenelData);
                                Console.WriteLine($"[RoomService] ✅ 通知新玩家: {existingPlayer.SteamName} 在场景 {existingPlayer.CurrentScenelData.SceneName}");
                                
                                // 然后发送位置（位置同步会触发角色创建）
                                var lastPosition = _unitySyncService.GetLastPosition(existingPlayer.SteamId);
                                if (lastPosition != null)
                                {
                                    newPlayerContext.Call<IPlayerClientService>()
                                        .OnPlayerUnitySyncReceived(lastPosition);
                                    Console.WriteLine($"[RoomService] ✅ 发送 {existingPlayer.SteamName} 的位置给 {player.SteamName}");
                                }
                                else
                                {
                                    Console.WriteLine($"[RoomService] ⚠️ {existingPlayer.SteamName} 的位置缓存为空，等待首次位置同步");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[RoomService] ⚠️ {existingPlayer.SteamName} 不在场景中，跳过场景通知");
                            }
                        }
                        
                        // 🔥 发送装备数据给新玩家
                        _equipmentService?.SendAllEquipmentDataToPlayer(client.ClientId, request.RoomId);
                        
                        // 🔥 发送武器数据给新玩家
                        _weaponSyncService?.SendAllWeaponDataToPlayer(client.ClientId, request.RoomId);
                    }
                }
                
                // 2. 🔥 通知房间内所有其他玩家：新玩家加入了
                Console.WriteLine($"[RoomService] ========== 开始通知现有玩家 ==========");
                Console.WriteLine($"[RoomService] 房间内玩家总数: {roomPlayers.Count()}");
                
                int notifiedCount = 0;
                foreach (var p in roomPlayers)
                {
                    Console.WriteLine($"[RoomService] 检查玩家: {p.SteamName} ({p.SteamId})");
                    
                    if (p.SteamId == player.SteamId)
                    {
                        Console.WriteLine($"[RoomService] 跳过新玩家自己: {p.SteamName}");
                        continue;
                    }
                    
                    Console.WriteLine($"[RoomService] 尝试获取玩家 {p.SteamName} 的 ClientId...");
                    var clientId = _playerManager.GetClientIdBySteamId(p.SteamId);
                    
                    if (string.IsNullOrEmpty(clientId))
                    {
                        Console.WriteLine($"[RoomService] ⚠️ 玩家 {p.SteamName} 的 ClientId 为空！");
                        continue;
                    }
                    
                    Console.WriteLine($"[RoomService] ClientId: {clientId}，尝试获取客户端上下文...");
                    var playerContext = _server.GetClientContext(clientId);
                    
                    if (playerContext == null)
                    {
                        Console.WriteLine($"[RoomService] ⚠️ 玩家 {p.SteamName} 的客户端上下文为 null！");
                        continue;
                    }
                    
                    Console.WriteLine($"[RoomService] ✅ 找到玩家 {p.SteamName} 的客户端上下文");
                    
                    // 通知房间成员关系
                    playerContext.Call<IRoomClientService>()
                        .OnPlayerJoinedRoom(player, result.Room);
                    Console.WriteLine($"[RoomService] ✅ 已通知玩家 {p.SteamName}: 新玩家 {player.SteamName} 加入房间");
                    Console.WriteLine($"[RoomService] ✅ 新玩家 {player.SteamName} 的头像URL: {player.AvatarUrl ?? "(null)"}");
                    notifiedCount++;
                    
                    // 🔥 如果新玩家已经在场景中，发送场景进入事件和位置
                    if (!string.IsNullOrEmpty(player.CurrentScenelData.SceneName))
                    {
                        playerContext.Call<ISceneClientService>()
                            .OnPlayerEnteredScene(player, player.CurrentScenelData);
                        Console.WriteLine($"[RoomService] ✅ 通知 {p.SteamName}: 新玩家 {player.SteamName} 在场景 {player.CurrentScenelData.SceneName}");
                        
                        // 发送新玩家的位置
                        var newPlayerLastPos = _unitySyncService.GetLastPosition(player.SteamId);
                        if (newPlayerLastPos != null)
                        {
                            playerContext.Call<IPlayerClientService>()
                                .OnPlayerUnitySyncReceived(newPlayerLastPos);
                            Console.WriteLine($"[RoomService] ✅ 已发送新玩家 {player.SteamName} 的位置给 {p.SteamName}");
                        }
                    }
                }
                
                Console.WriteLine($"[RoomService] ========== 通知完成，共通知 {notifiedCount} 个玩家 ==========");

                Console.WriteLine($"[RoomService] Player {player.SteamName} joined room {request.RoomId}");
                
                // 发布玩家加入房间事件
                ServerEventPublisher.PublishPlayerJoinedRoom(result.Room, player);
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
                // 清除玩家的位置缓存和场景数据
                _unitySyncService.ClearPlayerPosition(player.SteamId);
                _playerManager.UpdatePlayerSceneDataByClientId(client.ClientId, new ScenelData("", ""));
                Console.WriteLine($"[RoomService] 已清除 {player.SteamName} 的位置缓存和场景数据");
                
                // 发布玩家离开房间事件
                ServerEventPublisher.PublishPlayerLeftRoom(room, player);
                
                // 通知房间内其他玩家
                var roomPlayers = _roomManager.GetRoomPlayers(room.RoomId);
                foreach (var p in roomPlayers)
                {
                    // 🔥 修复：使用 ClientId 而不是 SteamId
                    var clientId = _playerManager.GetClientIdBySteamId(p.SteamId);
                    if (!string.IsNullOrEmpty(clientId))
                    {
                        var playerContext = _server.GetClientContext(clientId);
                        if (playerContext != null)
                        {
                            playerContext.Call<IRoomClientService>()
                                .OnPlayerLeftRoom(player, room);
                            Console.WriteLine($"[RoomService] ✅ 通知玩家 {p.SteamName}: {player.SteamName} 离开房间");
                        }
                    }
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
            Console.WriteLine($"[RoomService] ========== GetRoomPlayers 被调用 ==========");
            Console.WriteLine($"[RoomService] 请求者: {requester.SteamName}");
            Console.WriteLine($"[RoomService] 房间ID: {roomId}");
            Console.WriteLine($"[RoomService] 玩家数量: {players.Length}");
            
            // 🔥 关键修复：主动向请求者发送房间内其他玩家的加入通知
            // 这样客户端的 PlayerManager 会创建 RemotePlayer
            var room = _roomManager.GetPlayerRoom(requester);
            Console.WriteLine($"[RoomService] 请求者所在房间: {(room != null ? room.RoomId : "null")}");
            if (room != null)
            {
                // 🔥 修复：使用 ClientId 而不是 SteamId
                var requesterClientId = _playerManager.GetClientIdBySteamId(requester.SteamId);
                if (!string.IsNullOrEmpty(requesterClientId))
                {
                    var requesterContext = _server.GetClientContext(requesterClientId);
                    if (requesterContext != null)
                    {
                        int notifiedPlayers = 0;
                        foreach (var otherPlayer in players)
                        {
                            // 跳过请求者自己
                            if (otherPlayer.SteamId == requester.SteamId)
                            {
                                Console.WriteLine($"[RoomService] GetRoomPlayers: 跳过请求者自己: {requester.SteamName}");
                                continue;
                            }

                            // 🔥 简化：只发送房间成员通知
                            requesterContext.Call<IRoomClientService>()
                                .OnPlayerJoinedRoom(otherPlayer, room);
                            Console.WriteLine($"[RoomService] GetRoomPlayers: ✅ 通知 {requester.SteamName} 房间内有玩家 {otherPlayer.SteamName} (AvatarUrl: {otherPlayer.AvatarUrl ?? "(null)"})");
                            notifiedPlayers++;

                            // 🔥 优化：如果对方在场景中，发送位置（不发送场景通知）
                            if (!string.IsNullOrEmpty(otherPlayer.CurrentScenelData.SceneName))
                            {
                                var lastPosition = _unitySyncService.GetLastPosition(otherPlayer.SteamId);
                                if (lastPosition != null)
                                {
                                    requesterContext.Call<IPlayerClientService>()
                                        .OnPlayerUnitySyncReceived(lastPosition);
                                    Console.WriteLine($"[RoomService] GetRoomPlayers: ✅ 发送 {otherPlayer.SteamName} 的位置给 {requester.SteamName}");
                                }
                                else
                                {
                                    Console.WriteLine($"[RoomService] GetRoomPlayers: ⚠️ {otherPlayer.SteamName} 无位置缓存，等待实时同步");
                                }
                            }
                        }
                        
                        Console.WriteLine($"[RoomService] GetRoomPlayers: ========== 共通知了 {notifiedPlayers} 个玩家 ==========");
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
            else
            {
                Console.WriteLine($"[RoomService] ⚠️ GetRoomPlayers: 请求者不在房间中");
            }

            Console.WriteLine($"[RoomService] ========== GetRoomPlayers 完成 ==========");
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
                // 🔥 修复：使用 ClientId 而不是 playerId (SteamId)
                var targetClientId = _playerManager.GetClientIdBySteamId(targetPlayer.SteamId);
                if (!string.IsNullOrEmpty(targetClientId))
                {
                    var targetContext = _server.GetClientContext(targetClientId);
                    if (targetContext != null)
                    {
                        targetContext.Call<IRoomClientService>()
                            .OnKickedFromRoom($"被房主 {player.SteamName} 踢出房间");
                        Console.WriteLine($"[RoomService] ✅ 通知玩家 {targetPlayer.SteamName} 被踢出房间");
                    }
                }

                Console.WriteLine($"[RoomService] Player {targetPlayer.SteamName} kicked by {player.SteamName}");
            }

            return await Task.FromResult(result);
        }

    }
}

