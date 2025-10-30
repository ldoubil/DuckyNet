using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.RPC;
using DuckyNet.Server.Managers;
using DuckyNet.Server.RPC;
using DuckyNet.Shared.Data;

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


        public SceneServiceImpl(RpcServer server, PlayerManager playerManager, RoomManager roomManager)
        {
            _server = server;
            _playerManager = playerManager;
            _roomManager = roomManager;
        }

        public Task<bool> EnterSceneAsync(IClientContext client, ScenelData scenelData)
        {
            var steamId = _playerManager.GetPlayer(client.ClientId);
            _playerManager.GetPlayerBySteamId(steamId?.SteamId ?? "");
          
            Console.WriteLine($"[SceneService] 玩家进入场景请求，client.ClientId={client.ClientId}, steamId={(steamId?.SteamId ?? "null")}, 场景=({scenelData?.SceneName},{scenelData?.SubSceneName})");
            if (steamId != null)
            {
                
                var roomId = _roomManager.GetPlayerRoom(steamId)?.RoomId ?? "";
                Console.WriteLine($"[SceneService] 玩家所在房间: roomId={roomId}");
                var roomPlayers = _playerManager.GetRoomPlayers(roomId);
                foreach (var p in roomPlayers)
                {
                    Console.WriteLine($"[SceneService] 通知玩家 {p.SteamName} (SteamId={p.SteamId}) 进入场景 ({scenelData?.SceneName},{scenelData?.SubSceneName})");
                    var clientId = _playerManager.GetClientIdBySteamId(p.SteamId);
                    if (clientId != null)
                    {
                        var clientContext = _server.GetClientContext(clientId);
                        if (clientContext != null)
                        {
                            clientContext.Call<ISceneClientService>().OnPlayerEnteredScene(p, scenelData);
                            Console.WriteLine($"[SceneService] 已调用 OnPlayerEnteredScene 给 {p.SteamId}");
                        }

                        else
                        {
                            Console.WriteLine($"[SceneService] ⚠️ 未找到客户端上下文: {p.SteamId}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"[SceneService] ⚠️ 未找到玩家信息, client.ClientId={client.ClientId}");
            }
            Console.WriteLine("[SceneService] EnterSceneAsync 完成");
            return Task.FromResult(true);
        }

        public Task<PlayerInfo[]> GetScenePlayersAsync(IClientContext client, ScenelData scenelData)
        {
            var playerInfo = _playerManager.GetPlayer(client.ClientId);
            if (playerInfo != null)
            {
                var roomId = _roomManager.GetPlayerRoom(playerInfo)?.RoomId ?? "";
                var players = _playerManager.GetRoomPlayers(roomId);
                // 匹配 scenelData.SceneName 和 scenelData.SubSceneName 
                var matchedPlayers = players.Where(p => p.CurrentScenelData.SceneName == scenelData.SceneName && p.CurrentScenelData.SubSceneName == scenelData.SubSceneName).ToArray();
                return Task.FromResult(matchedPlayers);
            }
            return Task.FromResult(Array.Empty<PlayerInfo>());
        }

        public Task<bool> LeaveSceneAsync(IClientContext client, ScenelData scenelData)
        {
            var steamId = _playerManager.GetPlayer(client.ClientId);
            if (steamId != null)
            {
                var roomId = _roomManager.GetPlayerRoom(steamId)?.RoomId ?? "";
                foreach (var p in _playerManager.GetRoomPlayers(roomId))
                {
                    var clientContext = _server.GetClientContext(p.SteamId);
                    if (clientContext != null)
                    {
                        clientContext.Call<ISceneClientService>().OnPlayerLeftScene(p, scenelData);
                    }
                }
            }
            return Task.FromResult(true);
        }
    }
}
