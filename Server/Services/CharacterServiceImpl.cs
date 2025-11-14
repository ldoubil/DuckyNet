using System;
using System.Threading.Tasks;
using DuckyNet.Server.Core;
using DuckyNet.Shared.Data;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Services;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 角色服务实现
    /// </summary>
    public class CharacterServiceImpl : ICharacterService
    {
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
                var player = ServerContext.Players.GetPlayer(steamId);
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
                var room = ServerContext.Rooms.GetPlayerRoom(player);
                if (room != null)
                {
                    ServerContext.Broadcast.BroadcastToRoomExcludeSelf(player, (target, targetContext) =>
                    {
                        targetContext.Call<ICharacterClientService>()
                            .OnPlayerAppearanceUpdated(steamId, appearanceData);
                    });
                    
                    Console.WriteLine($"[CharacterService] 已广播外观更新到房间 {room.RoomId}");
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
                // 通过 SteamId 查找玩家
                var player = ServerContext.Players.GetPlayerBySteamId(targetSteamId);

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
                var player = ServerContext.Players.GetPlayer(clientId);
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
                    var room = ServerContext.Rooms.GetPlayerRoom(player);
                    if (room != null && !string.IsNullOrEmpty(player.CurrentScenelData.SceneName))
                    {
                        // 通知房间内其他玩家角色已创建（重新发送场景进入事件）
                        ServerContext.Broadcast.BroadcastToRoomExcludeSelf(player, (target, targetContext) =>
                        {
                            targetContext.Call<ISceneClientService>()
                                .OnPlayerEnteredScene(player, player.CurrentScenelData);
                        });
                        
                        Console.WriteLine($"[CharacterService] 已通知房间 {room.RoomId} 角色创建");
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
    }
}
