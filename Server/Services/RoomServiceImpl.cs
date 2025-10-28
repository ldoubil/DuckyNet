using System;
using System.Threading.Tasks;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Managers;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;

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

        public RoomServiceImpl(RpcServer server, RoomManager roomManager, PlayerManager playerManager)
        {
            _server = server;
            _roomManager = roomManager;
            _playerManager = playerManager;
        }

        public async Task<RoomInfo[]> GetRoomListAsync(IClientContext client)
        {
            // 检查是否已登录
            if (!_playerManager.IsLoggedIn(client.ClientId))
            {
                throw new UnauthorizedAccessException("Not logged in");
            }

            var rooms = _roomManager.GetAllRooms();
            return await Task.FromResult(rooms);
        }

        public async Task<RoomOperationResult> CreateRoomAsync(IClientContext client, CreateRoomRequest request)
        {
            // 检查是否已登录
            if (!_playerManager.IsLoggedIn(client.ClientId))
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
            if (!_playerManager.IsLoggedIn(client.ClientId))
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

            var result = _roomManager.JoinRoom(player.SteamId, player.SteamName, request);

            if (result.Success && result.Room != null)
            {
                // 通知房间内所有玩家
                var roomPlayerIds = _roomManager.GetRoomPlayerIds(request.RoomId);
                foreach (var playerId in roomPlayerIds)
                {
                    var playerContext = _server.GetClientContext(playerId);
                    if (playerContext != null)
                    {
                        playerContext.Call<IRoomClientService>()
                            .OnPlayerJoinedRoom(player, result.Room);
                    }
                }

                Console.WriteLine($"[RoomService] Player {player.SteamName} joined room {request.RoomId}");
            }

            return await Task.FromResult(result);
        }

        public async Task<bool> LeaveRoomAsync(IClientContext client)
        {
            if (!_playerManager.IsLoggedIn(client.ClientId))
            {
                return false;
            }

            var player = _playerManager.GetPlayer(client.ClientId);
            if (player == null)
            {
                return false;
            }

            var room = _roomManager.LeaveRoom(player.SteamId);

            if (room != null)
            {
                // 通知房间内其他玩家
                var roomPlayerIds = _roomManager.GetRoomPlayerIds(room.RoomId);
                foreach (var playerId in roomPlayerIds)
                {
                    var playerContext = _server.GetClientContext(playerId);
                    if (playerContext != null)
                    {
                        playerContext.Call<IRoomClientService>()
                            .OnPlayerLeftRoom(player, room);
                    }
                }

                Console.WriteLine($"[RoomService] Player {player.SteamName} left room {room.RoomId}");
            }

            return await Task.FromResult(true);
        }

        public async Task<RoomInfo?> GetCurrentRoomAsync(IClientContext client)
        {
            if (!_playerManager.IsLoggedIn(client.ClientId))
            {
                return null;
            }

            var player = _playerManager.GetPlayer(client.ClientId);
            if (player == null)
            {
                return null;
            }

            var room = _roomManager.GetPlayerRoom(player.SteamId);
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
            if (!_playerManager.IsLoggedIn(client.ClientId))
            {
                return Array.Empty<PlayerInfo>();
            }

            var players = _playerManager.GetRoomPlayers(roomId);
            return await Task.FromResult(players);
        }

        public async Task<bool> KickPlayerAsync(IClientContext client, string playerId)
        {
            if (!_playerManager.IsLoggedIn(client.ClientId))
            {
                return false;
            }

            var player = _playerManager.GetPlayer(client.ClientId);
            if (player == null)
            {
                return false;
            }

            var result = _roomManager.KickPlayer(player.SteamId, playerId);

            if (result)
            {
                // 通知被踢的玩家
                var targetContext = _server.GetClientContext(playerId);
                if (targetContext != null)
                {
                    targetContext.Call<IRoomClientService>()
                        .OnKickedFromRoom("Kicked by host");
                }

                Console.WriteLine($"[RoomService] Player {playerId} kicked by {client.ClientId}");
            }

            return await Task.FromResult(result);
        }

    }
}

