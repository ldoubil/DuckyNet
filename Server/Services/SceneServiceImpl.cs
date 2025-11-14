using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.Services;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Server.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 场景服务实现
    /// 📌 核心逻辑：场景进入/离开时，广播给同房间的所有玩家
    /// </summary>
    public class SceneServiceImpl : ISceneService
    {

        /// <summary>
        /// 玩家进入场景
        /// 📢 广播逻辑：
        /// 1. 广播给房间内所有人：该玩家进入了场景
        /// 2. 给新进入的玩家发送：房间内所有其他玩家的状态（位置、皮肤、装备、武器）
        /// </summary>
        public Task<bool> EnterSceneAsync(IClientContext client, ScenelData scenelData)
        {
            var nonNullData = scenelData ?? new ScenelData("", "");
            var player = ServerContext.Players.GetPlayer(client.ClientId);
            
            if (player == null)
            {
                Console.WriteLine($"[SceneService] ⚠️ 未找到玩家信息, ClientId={client.ClientId}");
                return Task.FromResult(false);
            }

            // ✅ 防御性检查：验证玩家是否在房间中
            var room = ServerContext.Rooms.GetPlayerRoom(player);
            if (room == null)
            {
                Console.WriteLine($"[SceneService] ❌ 玩家 {player.SteamName} 不在任何房间中，无法进入场景");
                return Task.FromResult(false);
            }

            // ✅ 防御性检查：验证场景数据有效性
            if (string.IsNullOrEmpty(nonNullData.SceneName))
            {
                Console.WriteLine($"[SceneService] ❌ 场景名为空，玩家 {player.SteamName} 进入场景失败");
                return Task.FromResult(false);
            }

            // 1️⃣ 使用 SceneManager 更新场景数据
            if (!ServerContext.Scenes.EnterScene(client.ClientId, nonNullData))
            {
                return Task.FromResult(false);
            }
            
            // 2️⃣ 使用 BroadcastManager 广播给房间内所有玩家
            ServerContext.Broadcast.BroadcastToRoom(player, (target, targetContext) =>
            {
                try
                {
                    targetContext.Call<ISceneClientService>().OnPlayerEnteredScene(player, nonNullData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SceneService] ❌ 广播失败 {player.SteamName} → {target.SteamName}: {ex.Message}");
                }
            });
            
            // 3️⃣ 给新进入的玩家同步房间内其他玩家的状态
            SyncExistingPlayersToNewPlayer(client, player, nonNullData);
            
            return Task.FromResult(true);
        }

        /// <summary>
        /// 给新进入场景的玩家同步房间内已存在的其他玩家
        /// 📤 发送：玩家信息、场景位置、外观、装备、武器数据
        /// </summary>
        private void SyncExistingPlayersToNewPlayer(IClientContext newPlayerClient, PlayerInfo newPlayer, ScenelData scenelData)
        {
            // 获取房间
            var room = ServerContext.Rooms.GetPlayerRoom(newPlayer);
            if (room == null)
            {
                Console.WriteLine($"[SceneService] ⚠️ 玩家不在房间中，无法同步其他玩家: {newPlayer.SteamName}");
                return;
            }

            // 获取房间内所有玩家
            var roomPlayers = ServerContext.Players.GetRoomPlayers(room.RoomId);
            
            // 筛选出在同一场景且不是自己的玩家
            var existingPlayers = roomPlayers
                .Where(p => p.SteamId != newPlayer.SteamId && 
                           p.CurrentScenelData.SceneName == scenelData.SceneName &&
                           p.CurrentScenelData.SubSceneName == scenelData.SubSceneName)
                .ToList();

            if (existingPlayers.Count == 0)
            {
                Console.WriteLine($"[SceneService] 场景内没有其他玩家，无需同步: {newPlayer.SteamName}");
                return;
            }

            Console.WriteLine($"[SceneService] 同步 {existingPlayers.Count} 个现有玩家给 {newPlayer.SteamName}");

            // 给新玩家发送每个现有玩家的进入场景事件
            foreach (var existingPlayer in existingPlayers)
            {
                try
                {
                    newPlayerClient.Call<ISceneClientService>()
                        .OnPlayerEnteredScene(existingPlayer, existingPlayer.CurrentScenelData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SceneService] ❌ 同步失败 {existingPlayer.SteamName} → {newPlayer.SteamName}: {ex.Message}");
                }
            }
        }

        public Task<PlayerInfo[]> GetScenePlayersAsync(IClientContext client, ScenelData scenelData)
        {
            // 使用 SceneManager 获取场景玩家
            var players = ServerContext.Scenes.GetScenePlayers(client.ClientId, scenelData);
            return Task.FromResult(players);
        }

        /// <summary>
        /// 玩家离开场景
        /// 📢 广播逻辑：离开场景时，广播给房间内所有人（用于销毁角色）
        /// </summary>
        public Task<bool> LeaveSceneAsync(IClientContext client, ScenelData scenelData)
        {
            var player = ServerContext.Players.GetPlayer(client.ClientId);
            
            if (player == null)
            {
                Console.WriteLine($"[SceneService] ⚠️ 未找到玩家信息, ClientId={client.ClientId}");
                return Task.FromResult(false);
            }

            // 1️⃣ 使用 SceneManager 清除场景数据
            if (!ServerContext.Scenes.LeaveScene(client.ClientId, scenelData))
            {
                return Task.FromResult(false);
            }
            
            // 2️⃣ 使用 BroadcastManager 广播给房间内所有玩家
            ServerContext.Broadcast.BroadcastToRoom(player, (target, targetContext) =>
            {
                targetContext.Call<ISceneClientService>().OnPlayerLeftScene(player, scenelData);
                Console.WriteLine($"[SceneService] ✅ 通知 {target.SteamName}: {player.SteamName} 离开场景 {scenelData.SceneName}");
            });
            
            return Task.FromResult(true);
        }
    }
}
