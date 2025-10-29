using System;
using System.Threading.Tasks;
using DuckyNet.Server.Managers;
using DuckyNet.Server.RPC;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 角色服务实现
    /// </summary>
    public class CharacterServiceImpl : ICharacterService
    {
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;
        private readonly RpcServer _server;
        private readonly SceneServiceImpl? _sceneService;

        public CharacterServiceImpl(RpcServer server, PlayerManager playerManager, RoomManager roomManager, SceneServiceImpl? sceneService = null)
        {
            _server = server;
            _playerManager = playerManager;
            _roomManager = roomManager;
            _sceneService = sceneService;
        }

        public Task<bool> UpdateAppearanceAsync(IClientContext client, byte[] appearanceData)
        {
            var steamId = client.ClientId;
            if (string.IsNullOrEmpty(steamId))
            {
                Console.WriteLine($"[CharacterService] 更新外观失败: 无效的Client ID");
                return Task.FromResult(false);
            }

            try
            {
                var player = _playerManager.GetPlayer(steamId);
                if (player == null)
                {
                    Console.WriteLine($"[CharacterService] 更新外观失败: 玩家不存在 - {steamId}");
                    return Task.FromResult(false);
                }

                // 验证数据大小（最大10KB）
                if (appearanceData == null || appearanceData.Length == 0)
                {
                    Console.WriteLine($"[CharacterService] 更新外观失败: 数据为空 - {steamId}");
                    return Task.FromResult(false);
                }

                if (appearanceData.Length > 10240)
                {
                    Console.WriteLine($"[CharacterService] 更新外观失败: 数据过大 ({appearanceData.Length} bytes) - {steamId}");
                    return Task.FromResult(false);
                }

                // 更新玩家外观数据
                player.AppearanceData = appearanceData;
                Console.WriteLine($"[CharacterService] 外观已更新 ({appearanceData.Length} bytes) - {player.SteamName}({player.SteamId})");

                // 通知同房间的其他玩家
                var room = _roomManager.GetPlayerRoom(player.SteamId);
                if (room != null)
                {
                    NotifyAppearanceUpdate(room.RoomId, player.SteamId, appearanceData);
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CharacterService] 更新外观异常: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public Task<byte[]?> GetAppearanceAsync(IClientContext client, string targetSteamId)
        {
            try
            {
                // 查找目标玩家（通过 SteamId）
                PlayerInfo? player = null;
                var allPlayers = _playerManager.GetAllOnlinePlayers();
                foreach (var p in allPlayers)
                {
                    if (p.SteamId == targetSteamId)
                    {
                        player = p;
                        break;
                    }
                }

                if (player == null)
                {
                    Console.WriteLine($"[CharacterService] 获取外观失败: 玩家不存在 - {targetSteamId}");
                    return Task.FromResult<byte[]?>(null);
                }

                if (player.AppearanceData == null || player.AppearanceData.Length == 0)
                {
                    Console.WriteLine($"[CharacterService] 玩家未设置外观 - {targetSteamId}");
                    return Task.FromResult<byte[]?>(null);
                }

                Console.WriteLine($"[CharacterService] 返回外观数据 ({player.AppearanceData.Length} bytes) - {targetSteamId}");
                return Task.FromResult<byte[]?>(player.AppearanceData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CharacterService] 获取外观异常: {ex.Message}");
                return Task.FromResult<byte[]?>(null);
            }
        }

        public Task<bool> SetCharacterCreatedAsync(IClientContext client, bool hasCharacter)
        {
            var clientId = client.ClientId;
            if (string.IsNullOrEmpty(clientId))
            {
                Console.WriteLine($"[CharacterService] 设置角色状态失败: 无效的Client ID");
                return Task.FromResult(false);
            }

            try
            {
                var player = _playerManager.GetPlayer(clientId);
                if (player == null)
                {
                    Console.WriteLine($"[CharacterService] 设置角色状态失败: 玩家不存在 - {clientId}");
                    return Task.FromResult(false);
                }

                var wasHasCharacter = player.HasCharacter;
                player.HasCharacter = hasCharacter;
                Console.WriteLine($"[CharacterService] 角色状态已更新: {wasHasCharacter} -> {hasCharacter} - {player.SteamName}({player.SteamId})");

                // 如果角色刚被创建，且玩家在场景中，通知同房间的其他玩家
                if (hasCharacter && !wasHasCharacter)
                {
                    var room = _roomManager.GetPlayerRoom(player.SteamId);
                    if (room != null)
                    {
                        // 获取场景服务，通知其他玩家这个玩家已创建角色
                        NotifyCharacterCreated(room.RoomId, player);
                    }
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CharacterService] 设置角色状态异常: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 获取玩家场景名称（辅助方法）
        /// </summary>
        private string? GetPlayerSceneName(string steamId)
        {
            try
            {
                if (_sceneService == null) return null;
                
                // 使用反射访问 SceneServiceImpl 的内部字段获取场景名称
                var sceneServiceType = typeof(SceneServiceImpl);
                var playerScenesField = sceneServiceType.GetField("_playerScenes", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (playerScenesField != null)
                {
                    var playerScenes = playerScenesField.GetValue(_sceneService) as System.Collections.Generic.Dictionary<string, string>;
                    if (playerScenes != null && playerScenes.TryGetValue(steamId, out var sceneName))
                    {
                        return sceneName;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CharacterService] 获取玩家场景名称失败: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// 通知房间内的其他玩家角色已创建
        /// </summary>
        private void NotifyCharacterCreated(string roomId, PlayerInfo player)
        {
            try
            {
                var roomPlayerIds = _roomManager.GetRoomPlayerIds(roomId);
                int notifiedCount = 0;

                foreach (var playerSteamId in roomPlayerIds)
                {
                    // 不通知自己
                    if (playerSteamId == player.SteamId)
                        continue;

                    try
                    {
                        var clientContext = _server.GetClientContext(playerSteamId);
                        if (clientContext != null)
                        {
                            // 如果玩家在场景中，重新发送场景信息（更新 HasCharacter 状态）
                            // 获取玩家的场景名称
                            var sceneName = GetPlayerSceneName(player.SteamId);
                            
                            if (!string.IsNullOrEmpty(sceneName))
                            {
                                // 创建更新后的场景信息（只需要场景名称）
                                var playerSceneInfo = new Shared.Services.PlayerSceneInfo
                                {
                                    SteamId = player.SteamId,
                                    PlayerInfo = player,
                                    SceneName = sceneName,
                                    HasCharacter = true   // 角色已创建
                                };
                                
                                // 直接通过 RPC 通知客户端
                                try
                                {
                                    clientContext.Call<Shared.Services.ISceneClientService>().OnPlayerEnteredScene(playerSceneInfo);
                                    notifiedCount++;
                                }
                                catch (Exception ex2)
                                {
                                    Console.WriteLine($"[CharacterService] 发送玩家进入场景通知失败: {ex2.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CharacterService] 通知玩家 {playerSteamId} 角色创建失败: {ex.Message}");
                    }
                }

                if (notifiedCount > 0)
                {
                    Console.WriteLine($"[CharacterService] 已通知 {notifiedCount} 个玩家角色创建 - {player.SteamName}({player.SteamId})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CharacterService] 通知角色创建异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知房间内的其他玩家外观更新
        /// </summary>
        private void NotifyAppearanceUpdate(string roomId, string steamId, byte[] appearanceData)
        {
            try
            {
                var roomPlayerIds = _roomManager.GetRoomPlayerIds(roomId);
                int notifiedCount = 0;

                foreach (var playerSteamId in roomPlayerIds)
                {
                    // 不通知自己
                    if (playerSteamId == steamId)
                        continue;

                    try
                    {
                        var clientContext = _server.GetClientContext(playerSteamId);
                        if (clientContext != null)
                        {
                            clientContext.Call<ICharacterClientService>().OnPlayerAppearanceUpdated(steamId, appearanceData);
                            notifiedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CharacterService] 通知玩家 {playerSteamId} 外观更新失败: {ex.Message}");
                    }
                }

                if (notifiedCount > 0)
                {
                    Console.WriteLine($"[CharacterService] 已通知 {notifiedCount} 个玩家外观更新 - {steamId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CharacterService] 通知外观更新异常: {ex.Message}");
            }
        }
    }
}

