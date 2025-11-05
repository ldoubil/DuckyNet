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
    /// 📌 核心逻辑：场景进入/离开时，广播给同房间的所有玩家
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

        /// <summary>
        /// 玩家进入场景
        /// 📢 广播逻辑：
        /// 1. 广播给房间内所有人：该玩家进入了场景
        /// 2. 给新进入的玩家发送：房间内所有其他玩家的状态（位置、皮肤、装备、武器）
        /// </summary>
        public Task<bool> EnterSceneAsync(IClientContext client, ScenelData scenelData)
        {
            var nonNullData = scenelData ?? new ScenelData("", "");
            var player = _playerManager.GetPlayer(client.ClientId);
            
            if (player == null)
            {
                Console.WriteLine($"[SceneService] ⚠️ 未找到玩家信息, ClientId={client.ClientId}");
                return Task.FromResult(false);
            }

            Console.WriteLine($"[SceneService] {player.SteamName} 进入场景: {nonNullData.SceneName}/{nonNullData.SubSceneName}");
            
            // 1️⃣ 更新玩家的场景数据（影响位置同步筛选）
            _playerManager.UpdatePlayerSceneDataByClientId(client.ClientId, nonNullData);
            
            // 2️⃣ 广播给房间内所有玩家：该玩家进入了场景（包括自己）
            BroadcastToRoom(player, (target, targetContext) =>
            {
                targetContext.Call<ISceneClientService>().OnPlayerEnteredScene(player, nonNullData);
                Console.WriteLine($"[SceneService] ✅ 通知 {target.SteamName}: {player.SteamName} 进入场景");
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
            var room = _roomManager.GetPlayerRoom(newPlayer);
            if (room == null)
            {
                Console.WriteLine($"[SceneService] ⚠️ 玩家不在房间中，无法同步其他玩家: {newPlayer.SteamName}");
                return;
            }

            // 获取房间内所有玩家
            var roomPlayers = _playerManager.GetRoomPlayers(room.RoomId);
            
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

            Console.WriteLine($"[SceneService] 📤 开始同步场景内现有玩家给 {newPlayer.SteamName}: {existingPlayers.Count} 个玩家");

            // 给新玩家发送每个现有玩家的进入场景事件
            // 客户端会根据这些事件创建 RemotePlayer 和角色
            foreach (var existingPlayer in existingPlayers)
            {
                try
                {
                    newPlayerClient.Call<ISceneClientService>()
                        .OnPlayerEnteredScene(existingPlayer, existingPlayer.CurrentScenelData);
                    
                    Console.WriteLine($"[SceneService] ✅ 已同步玩家 {existingPlayer.SteamName} 的状态给 {newPlayer.SteamName}");
                    Console.WriteLine($"[SceneService]   - 外观数据: {(existingPlayer.AppearanceData != null ? "已包含" : "空")}");
                    Console.WriteLine($"[SceneService]   - 装备数据: {existingPlayer.EquipmentData.GetEquippedCount()} 件");
                    Console.WriteLine($"[SceneService]   - 武器数据: {(existingPlayer.WeaponData != null ? existingPlayer.WeaponData.GetEquippedCount() + " 件" : "空")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SceneService] ❌ 同步玩家失败: {existingPlayer.SteamName}, 错误: {ex.Message}");
                }
            }

            Console.WriteLine($"[SceneService] ✅ 场景内玩家同步完成: {newPlayer.SteamName}");
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

        /// <summary>
        /// 玩家离开场景
        /// 📢 广播逻辑：离开场景时，广播给房间内所有人（用于销毁角色）
        /// </summary>
        public Task<bool> LeaveSceneAsync(IClientContext client, ScenelData scenelData)
        {
            var player = _playerManager.GetPlayer(client.ClientId);
            
            if (player == null)
            {
                Console.WriteLine($"[SceneService] ⚠️ 未找到玩家信息, ClientId={client.ClientId}");
                return Task.FromResult(false);
            }

            Console.WriteLine($"[SceneService] {player.SteamName} 离开场景: {scenelData.SceneName}/{scenelData.SubSceneName}");
            
            // 1️⃣ 清除玩家的场景数据（重要！影响位置同步过滤）
            _playerManager.UpdatePlayerSceneDataByClientId(client.ClientId, new ScenelData("", ""));
            
            // 2️⃣ 广播给房间内所有玩家（用于销毁角色）
            BroadcastToRoom(player, (target, targetContext) =>
            {
                targetContext.Call<ISceneClientService>().OnPlayerLeftScene(player, scenelData);
                Console.WriteLine($"[SceneService] ✅ 通知 {target.SteamName}: {player.SteamName} 离开场景 {scenelData.SceneName}");
            });
            
            return Task.FromResult(true);
        }

        /// <summary>
        /// 向房间内所有玩家广播消息
        /// </summary>
        /// <param name="player">触发事件的玩家</param>
        /// <param name="action">广播动作（目标玩家，目标客户端上下文）</param>
        private void BroadcastToRoom(PlayerInfo player, Action<PlayerInfo, IClientContext> action)
        {
            // 获取玩家所在房间
            var room = _roomManager.GetPlayerRoom(player);
            if (room == null)
            {
                Console.WriteLine($"[SceneService] ⚠️ 玩家 {player.SteamName} 不在任何房间中");
                return;
            }

            // 遍历房间内所有玩家
            var roomPlayers = _playerManager.GetRoomPlayers(room.RoomId);
            foreach (var target in roomPlayers)
            {
                // 获取目标玩家的客户端ID
                var targetClientId = _playerManager.GetClientIdBySteamId(target.SteamId);
                if (string.IsNullOrEmpty(targetClientId))
                {
                    continue;
                }

                // 获取客户端上下文并执行广播动作
                var targetContext = _server.GetClientContext(targetClientId);
                if (targetContext != null)
                {
                    action(target, targetContext);
                }
            }
        }
    }
}
