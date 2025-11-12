using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.RPC;
using DuckyNet.Server.Core;
using DuckyNet.Server.Managers;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// NPC 同步服务实现（简化架构：玩家 → NPC 列表）
    /// </summary>
    public class NpcSyncServiceImpl : INpcSyncService
    {
        private readonly PlayerNpcManager _playerNpcManager;
        private readonly NpcVisibilityTracker _visibilityTracker;

        public NpcSyncServiceImpl(PlayerNpcManager playerNpcManager, NpcVisibilityTracker visibilityTracker)
        {
            _playerNpcManager = playerNpcManager ?? throw new ArgumentNullException(nameof(playerNpcManager));
            _visibilityTracker = visibilityTracker ?? throw new ArgumentNullException(nameof(visibilityTracker));
        }

        /// <summary>
        /// 客户端通知 NPC 生成（记录并主动推送给范围内玩家）
        /// </summary>
        public async Task NotifyNpcSpawned(IClientContext client, NpcSpawnData spawnData)
        {
            try
            {
                var player = ServerContext.Players.GetPlayer(client.ClientId);
                if (player == null) return;

                Console.WriteLine($"[NpcSyncService] 📥 收到 NPC 生成: {spawnData.NpcType} (ID: {spawnData.NpcId}, 来自: {player.SteamName})");

                // 1. 记录到玩家的 NPC 列表
                _playerNpcManager.AddNpc(player.SteamId, spawnData);

                // 2. 🔥 主动推送给范围内的其他玩家
                var scenePlayers = ServerContext.Players.GetScenePlayers(player, excludeSelf: true);
                if (scenePlayers.Count == 0)
                {
                    Console.WriteLine($"[NpcSyncService] ✅ NPC 已记录（无其他玩家在场景）");
                    return;
                }

                // 获取场景所有 NPC（用于可见性计算）
                var allNpcs = _playerNpcManager.GetSceneNpcs(
                    player.CurrentScenelData?.SceneName ?? "", 
                    player.CurrentScenelData?.SubSceneName ?? ""
                );

                // 对每个玩家检查可见性并推送
                int pushedCount = 0;
                foreach (var targetPlayer in scenePlayers)
                {
                    var targetClientId = ServerContext.Players.GetClientIdBySteamId(targetPlayer.SteamId);
                    if (targetClientId == null) continue;

                    // 更新可见性
                    var change = _visibilityTracker.UpdatePlayerVisibility(
                        targetClientId,
                        targetPlayer,
                        allNpcs
                    );

                    // 如果新 NPC 在该玩家范围内，推送
                    if (change.EnteredRange.Contains(spawnData.NpcId))
                    {
                        ServerContext.Broadcast.CallClientTyped<INpcSyncClientService>(targetPlayer,
                            service => service.OnNpcSpawned(spawnData));
                        pushedCount++;
                        Console.WriteLine($"[NpcSyncService] 🚀 主动推送 NPC {spawnData.NpcId} 给 {targetPlayer.SteamName}");
                    }
                }

                Console.WriteLine($"[NpcSyncService] ✅ NPC 已记录并推送给 {pushedCount} 个玩家");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NpcSyncService] 处理 NPC 生成失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 客户端通知 NPC 位置更新（单个 - 已废弃，使用批量更新）
        /// </summary>
        public async Task NotifyNpcTransform(IClientContext client, NpcTransformData transformData)
        {
            try
            {
                // 转换为批量数据
                var batchData = new NpcBatchTransformData
                {
                    Count = 1,
                    NpcIds = new[] { transformData.NpcId },
                    PositionsX = new[] { transformData.PositionX },
                    PositionsY = new[] { transformData.PositionY },
                    PositionsZ = new[] { transformData.PositionZ },
                    RotationsY = new[] { transformData.RotationY }
                };

                // 调用批量更新
                await NotifyNpcBatchTransform(client, batchData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NpcSyncService] 处理 NPC 位置更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 客户端通知 NPC 批量位置更新（带范围过滤）
        /// </summary>
        public async Task NotifyNpcBatchTransform(IClientContext client, NpcBatchTransformData batchData)
        {
            try
            {
                var player = ServerContext.Players.GetPlayer(client.ClientId);
                if (player == null || batchData.Count == 0) return;

                // 🔥 1. 先更新服务器记录的 NPC 位置（即使没有其他玩家也要更新！）
                for (int i = 0; i < batchData.Count; i++)
                {
                    _playerNpcManager.UpdateNpcPosition(
                        batchData.NpcIds[i],
                        batchData.PositionsX[i],
                        batchData.PositionsY[i],
                        batchData.PositionsZ[i],
                        batchData.RotationsY[i]
                    );
                }

                // 2. 获取同场景的其他玩家
                var scenePlayers = ServerContext.Players.GetScenePlayers(player, excludeSelf: true);
                if (scenePlayers.Count == 0) return; // 没有其他玩家，无需广播

                // 3. 获取场景所有玩家的 NPC（用于可见性计算）
                var allNpcs = _playerNpcManager.GetSceneNpcs(
                    player.CurrentScenelData?.SceneName ?? "", 
                    player.CurrentScenelData?.SubSceneName ?? ""
                );

                // 对每个玩家单独过滤和发送
                foreach (var targetPlayer in scenePlayers)
                {
                    // 获取该玩家的客户端 ID
                    var targetClientId = ServerContext.Players.GetClientIdBySteamId(targetPlayer.SteamId);
                    if (targetClientId == null) continue;

                    // 🔥 更新可见性（检测进入/离开范围的 NPC）
                    var change = _visibilityTracker.UpdatePlayerVisibility(
                        targetClientId,
                        targetPlayer,
                        allNpcs
                    );

                    // 处理新进入范围的 NPC（发送创建）
                    foreach (var enteredNpcId in change.EnteredRange)
                    {
                        var enteredNpc = allNpcs.FirstOrDefault(n => n.NpcId == enteredNpcId);
                        if (enteredNpc != null)
                        {
                            ServerContext.Broadcast.CallClientTyped<INpcSyncClientService>(targetPlayer,
                                service => service.OnNpcSpawned(enteredNpc));
                            Console.WriteLine($"[NpcSyncService] 🆕 NPC {enteredNpcId} 进入 {targetPlayer.SteamName} 范围");
                        }
                    }

                    // 处理离开范围的 NPC（发送销毁）
                    foreach (var leftNpcId in change.LeftRange)
                    {
                        ServerContext.Broadcast.CallClientTyped<INpcSyncClientService>(targetPlayer,
                            service => service.OnNpcDestroyed(new NpcDestroyData { NpcId = leftNpcId, Reason = 1 }));
                        Console.WriteLine($"[NpcSyncService] 🗑️ NPC {leftNpcId} 离开 {targetPlayer.SteamName} 范围");
                    }

                    // 过滤在范围内的 NPC（只发送位置更新）
                    var visibleIndices = _visibilityTracker.FilterVisibleNpcIndices(targetClientId, batchData.NpcIds);

                    if (visibleIndices.Count > 0)
                    {
                        // 构建过滤后的批量数据
                        var filteredBatch = new NpcBatchTransformData
                        {
                            Count = visibleIndices.Count,
                            NpcIds = visibleIndices.Select(i => batchData.NpcIds[i]).ToArray(),
                            PositionsX = visibleIndices.Select(i => batchData.PositionsX[i]).ToArray(),
                            PositionsY = visibleIndices.Select(i => batchData.PositionsY[i]).ToArray(),
                            PositionsZ = visibleIndices.Select(i => batchData.PositionsZ[i]).ToArray(),
                            RotationsY = visibleIndices.Select(i => batchData.RotationsY[i]).ToArray()
                        };

                        // 发送给目标玩家
                        ServerContext.Broadcast.CallClientTyped<INpcSyncClientService>(targetPlayer,
                            service => service.OnNpcBatchTransform(filteredBatch));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NpcSyncService] 处理批量位置更新失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 客户端通知 NPC 销毁
        /// </summary>
        public async Task NotifyNpcDestroyed(IClientContext client, NpcDestroyData destroyData)
        {
            try
            {
                var player = ServerContext.Players.GetPlayer(client.ClientId);
                if (player == null) return;

                Console.WriteLine($"[NpcSyncService] 🗑️ 收到 NPC 销毁: {destroyData.NpcId} (来自: {player.SteamName})");

                // 从玩家的 NPC 列表中移除
                _playerNpcManager.RemoveNpc(destroyData.NpcId);

                // 广播给同场景的其他玩家
                ServerContext.Broadcast.BroadcastToSceneTyped<INpcSyncClientService>(player, 
                    service => service.OnNpcDestroyed(destroyData), 
                    excludeSelf: true);

                Console.WriteLine($"[NpcSyncService] ✅ NPC 销毁已广播");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NpcSyncService] 处理 NPC 销毁失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 玩家请求场景内所有 NPC（中途加入时 - 带范围过滤）
        /// </summary>
        public Task<NpcSpawnData[]> RequestSceneNpcs(IClientContext client, string sceneName, string subSceneName)
        {
            try
            {
                var player = ServerContext.Players.GetPlayer(client.ClientId);
                if (player == null)
                {
                    Console.WriteLine($"[NpcSyncService] ⚠️ 未找到玩家: {client.ClientId}");
                    return Task.FromResult(Array.Empty<NpcSpawnData>());
                }

                Console.WriteLine($"[NpcSyncService] 📥 玩家请求场景 NPC: {player.SteamName} → {sceneName}/{subSceneName}");

                // 获取场景所有玩家的 NPC
                var allNpcs = _playerNpcManager.GetSceneNpcs(sceneName, subSceneName);

                // 🔥 初始化该玩家的可见性（重要！）
                var change = _visibilityTracker.UpdatePlayerVisibility(
                    client.ClientId,
                    player,
                    allNpcs
                );

                // 只返回可见范围内的 NPC
                var visibleNpcs = allNpcs
                    .Where(n => change.CurrentVisible.Contains(n.NpcId))
                    .ToArray();

                Console.WriteLine($"[NpcSyncService] ✅ 返回 {visibleNpcs.Length}/{allNpcs.Count} 个可见 NPC");

                return Task.FromResult(visibleNpcs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NpcSyncService] 请求场景 NPC 失败: {ex.Message}");
                return Task.FromResult(Array.Empty<NpcSpawnData>());
            }
        }

        /// <summary>
        /// 请求单个 NPC 信息（按需加载）
        /// </summary>
        public Task<NpcSpawnData?> RequestSingleNpc(IClientContext client, string npcId)
        {
            try
            {
                var player = ServerContext.Players.GetPlayer(client.ClientId);
                if (player == null)
                {
                    Console.WriteLine($"[NpcSyncService] ⚠️ 未找到玩家: {client.ClientId}");
                    return Task.FromResult<NpcSpawnData?>(null);
                }

                Console.WriteLine($"[NpcSyncService] 📥 玩家请求单个 NPC: {player.SteamName} → {npcId}");

                // 从所有玩家的 NPC 中查找
                var npc = _playerNpcManager.GetNpcById(npcId);
                if (npc == null)
                {
                    Console.WriteLine($"[NpcSyncService] ⚠️ NPC 不存在: {npcId}");
                    return Task.FromResult<NpcSpawnData?>(null);
                }

                // 检查可见性（只返回范围内的 NPC）
                var distance = CalculateDistance(player, npc);
                if (distance > _visibilityTracker.SyncRange)
                {
                    Console.WriteLine($"[NpcSyncService] ⚠️ NPC 超出范围: {npcId} (距离: {distance:F1}m)");
                    return Task.FromResult<NpcSpawnData?>(null);
                }

                Console.WriteLine($"[NpcSyncService] ✅ 返回单个 NPC: {npcId} (距离: {distance:F1}m)");
                return Task.FromResult<NpcSpawnData?>(npc);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NpcSyncService] 请求单个 NPC 失败: {ex.Message}");
                return Task.FromResult<NpcSpawnData?>(null);
            }
        }

        /// <summary>
        /// 计算玩家与 NPC 的距离
        /// </summary>
        private float CalculateDistance(PlayerInfo player, NpcSpawnData npc)
        {
            // 从 SceneManager 缓存中获取玩家位置
            var playerPosNullable = ServerContext.Scenes.GetPlayerPosition(player.SteamId);
            if (!playerPosNullable.HasValue)
            {
                return float.MaxValue;
            }

            var playerPos = playerPosNullable.Value;
            float dx = playerPos.X - npc.PositionX;
            float dy = playerPos.Y - npc.PositionY;
            float dz = playerPos.Z - npc.PositionZ;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}

