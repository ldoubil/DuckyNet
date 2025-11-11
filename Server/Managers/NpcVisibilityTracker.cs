using System;
using System.Collections.Generic;
using System.Linq;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Server.Core;

namespace DuckyNet.Server.Managers
{
    /// <summary>
    /// NPC 可见性追踪器 - 管理每个玩家能看到哪些 NPC
    /// 
    /// 功能：
    /// 1. 追踪玩家可见的 NPC 集合
    /// 2. 计算进入/离开范围的 NPC
    /// 3. 与热区系统集成
    /// </summary>
    public class NpcVisibilityTracker
    {
        // 每个玩家当前可见的 NPC 集合
        private readonly Dictionary<string, HashSet<string>> _playerVisibleNpcs = new Dictionary<string, HashSet<string>>();

        // 配置
        public float SyncRange { get; set; } = 100f; // 同步范围

        public NpcVisibilityTracker()
        {
            Console.WriteLine("[NpcVisibilityTracker] ✅ 使用基础范围检查（100m）");
        }

        /// <summary>
        /// 计算玩家当前应该看到的 NPC（集成热区系统）
        /// </summary>
        public HashSet<string> CalculateVisibleNpcs(
            PlayerInfo player, 
            List<NpcSpawnData> allNpcs)
        {
            var visible = new HashSet<string>();

            // 玩家位置（从 SceneManager 缓存中获取）
            var playerPosNullable = ServerContext.Scenes.GetPlayerPosition(player.SteamId);
            if (!playerPosNullable.HasValue)
            {
                Console.WriteLine($"⚠️ [NpcVisibilityTracker] 玩家 {player.SteamName} 位置未缓存！");
                return visible;
            }
            
            var playerPos = playerPosNullable.Value;
            Console.WriteLine($"[NpcVisibilityTracker] 玩家 {player.SteamName} 位置: ({playerPos.X:F2}, {playerPos.Y:F2}, {playerPos.Z:F2})");

            // 获取场景信息
            var sceneName = player.CurrentScenelData?.SceneName ?? "";
            var subSceneName = player.CurrentScenelData?.SubSceneName ?? "";

            foreach (var npc in allNpcs)
            {
                var npcPos = new Vector3Data(npc.PositionX, npc.PositionY, npc.PositionZ);

                // 基础范围检查
                float distance = Distance(playerPos, npcPos);
                bool inPlayerRange = distance <= SyncRange;

                if (inPlayerRange)
                {
                    visible.Add(npc.NpcId);
                    Console.WriteLine($"  → NPC {npc.NpcId} 在范围内: {distance:F2}m < {SyncRange}m");
                }
                else
                {
                    Console.WriteLine($"  → NPC {npc.NpcId} 超出范围: {distance:F2}m > {SyncRange}m");
                }
            }

            return visible;
        }

        /// <summary>
        /// 更新玩家的可见 NPC 集合，返回变化
        /// </summary>
        public VisibilityChange UpdatePlayerVisibility(
            string playerId,
            PlayerInfo player,
            List<NpcSpawnData> allNpcs)
        {
            var currentVisible = CalculateVisibleNpcs(player, allNpcs);

            // 获取上次可见的 NPC
            if (!_playerVisibleNpcs.TryGetValue(playerId, out var lastVisible))
            {
                lastVisible = new HashSet<string>();
                _playerVisibleNpcs[playerId] = lastVisible;
            }

            // 计算变化
            var entered = currentVisible.Except(lastVisible).ToList();
            var left = lastVisible.Except(currentVisible).ToList();

            // 更新追踪
            _playerVisibleNpcs[playerId] = currentVisible;

            return new VisibilityChange
            {
                EnteredRange = entered,
                LeftRange = left,
                CurrentVisible = currentVisible.ToList()
            };
        }

        /// <summary>
        /// 过滤在范围内的 NPC（用于批量位置更新）
        /// </summary>
        public List<int> FilterVisibleNpcIndices(
            string playerId,
            string[] npcIds)
        {
            if (!_playerVisibleNpcs.TryGetValue(playerId, out var visible))
                return new List<int>();

            var indices = new List<int>();
            for (int i = 0; i < npcIds.Length; i++)
            {
                if (visible.Contains(npcIds[i]))
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        /// <summary>
        /// 移除玩家的追踪（断开连接时）
        /// </summary>
        public void RemovePlayer(string playerId)
        {
            _playerVisibleNpcs.Remove(playerId);
        }

        /// <summary>
        /// 清理场景内的 NPC 追踪（场景卸载时）
        /// </summary>
        public void ClearSceneNpcs(string sceneName, string subSceneName, List<string> npcIds)
        {
            foreach (var visibleSet in _playerVisibleNpcs.Values)
            {
                foreach (var npcId in npcIds)
                {
                    visibleSet.Remove(npcId);
                }
            }
        }

        /// <summary>
        /// 计算两点距离
        /// </summary>
        private float Distance(Vector3Data a, Vector3Data b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public VisibilityTrackerStats GetStats()
        {
            return new VisibilityTrackerStats
            {
                TrackedPlayers = _playerVisibleNpcs.Count,
                TotalVisibleNpcs = _playerVisibleNpcs.Values.Sum(s => s.Count),
                AvgNpcsPerPlayer = _playerVisibleNpcs.Count > 0 
                    ? _playerVisibleNpcs.Values.Average(s => s.Count) 
                    : 0
            };
        }
    }

    /// <summary>
    /// 可见性变化
    /// </summary>
    public class VisibilityChange
    {
        public List<string> EnteredRange { get; set; } = new List<string>();
        public List<string> LeftRange { get; set; } = new List<string>();
        public List<string> CurrentVisible { get; set; } = new List<string>();
    }

    /// <summary>
    /// 可见性追踪统计
    /// </summary>
    public struct VisibilityTrackerStats
    {
        public int TrackedPlayers;
        public int TotalVisibleNpcs;
        public double AvgNpcsPerPlayer;
    }

}

