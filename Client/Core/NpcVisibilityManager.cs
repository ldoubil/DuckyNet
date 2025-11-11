using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// NPC 可见性管理器（客户端）
    /// 
    /// 功能：
    /// 1. 只同步有变化的 NPC（位置/旋转变化检测）
    /// 2. 基于距离的同步范围限制（100m）
    /// 3. 变化检测（避免无效同步）
    /// </summary>
    public class NpcVisibilityManager : IDisposable
    {
        // 配置参数
        public float SyncRange { get; set; } = 100f; // 同步范围（米）
        public float PositionThreshold { get; set; } = 0.1f; // 位置变化阈值
        public float RotationThreshold { get; set; } = 5f; // 旋转变化阈值（度）

        // NPC 上次同步的状态
        private readonly Dictionary<string, NpcSyncState> _lastSyncStates = new Dictionary<string, NpcSyncState>();

        // 可见的远程 NPC 集合（用于追踪哪些是我们创建的）
        private readonly HashSet<string> _visibleRemoteNpcs = new HashSet<string>();

        /// <summary>
        /// 检测 NPC 是否有变化（位置或旋转）
        /// </summary>
        public bool HasChanged(string npcId, Vector3 position, float rotationY)
        {
            if (!_lastSyncStates.TryGetValue(npcId, out var lastState))
            {
                // 第一次同步，记录状态
                _lastSyncStates[npcId] = new NpcSyncState
                {
                    Position = position,
                    RotationY = rotationY,
                    LastSyncTime = Time.time
                };
                return true; // 第一次总是同步
            }

            // 检查位置变化
            float positionDelta = Vector3.Distance(position, lastState.Position);
            if (positionDelta > PositionThreshold)
            {
                // 更新状态
                lastState.Position = position;
                lastState.RotationY = rotationY;
                lastState.LastSyncTime = Time.time;
                return true;
            }

            // 检查旋转变化
            float rotationDelta = Mathf.Abs(Mathf.DeltaAngle(rotationY, lastState.RotationY));
            if (rotationDelta > RotationThreshold)
            {
                // 更新状态
                lastState.Position = position;
                lastState.RotationY = rotationY;
                lastState.LastSyncTime = Time.time;
                return true;
            }

            return false; // 没有显著变化
        }

        /// <summary>
        /// 检查 NPC 是否在同步范围内
        /// </summary>
        public bool IsInRange(Vector3 npcPosition, Vector3 playerPosition)
        {
            float distance = Vector3.Distance(npcPosition, playerPosition);
            return distance <= SyncRange;
        }

        /// <summary>
        /// 获取需要同步的 NPC 列表（过滤变化 + 范围）
        /// </summary>
        public List<string> GetNpcsToSync(Dictionary<string, NpcInfo> localNpcs, Vector3 playerPosition, List<Vector3>? remotePlayerPositions = null)
        {
            var npcsToSync = new List<string>();

            foreach (var kvp in localNpcs)
            {
                var npcId = kvp.Key;
                var npc = kvp.Value;

                if (!npc.IsAlive || npc.GameObject == null)
                    continue;

                var npcPos = npc.GameObject.transform.position;
                var npcRot = npc.GameObject.transform.rotation.eulerAngles.y;

                // 检查是否在基础同步范围内（服务器端已处理热区）
                bool inPlayerRange = IsInRange(npcPos, playerPosition);

                if (inPlayerRange)
                {
                    // 检查是否有变化
                    if (HasChanged(npcId, npcPos, npcRot))
                    {
                        npcsToSync.Add(npcId);
                    }
                }
            }

            return npcsToSync;
        }

        /// <summary>
        /// 标记远程 NPC 为可见
        /// </summary>
        public void MarkRemoteNpcVisible(string npcId)
        {
            _visibleRemoteNpcs.Add(npcId);
        }

        /// <summary>
        /// 标记远程 NPC 为不可见
        /// </summary>
        public void MarkRemoteNpcInvisible(string npcId)
        {
            _visibleRemoteNpcs.Remove(npcId);
        }

        /// <summary>
        /// 检查远程 NPC 是否可见
        /// </summary>
        public bool IsRemoteNpcVisible(string npcId)
        {
            return _visibleRemoteNpcs.Contains(npcId);
        }

        /// <summary>
        /// 清理 NPC 状态
        /// </summary>
        public void RemoveNpcState(string npcId)
        {
            _lastSyncStates.Remove(npcId);
            _visibleRemoteNpcs.Remove(npcId);
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public VisibilityStats GetStats()
        {
            return new VisibilityStats
            {
                TrackedNpcs = _lastSyncStates.Count,
                VisibleRemoteNpcs = _visibleRemoteNpcs.Count,
                SyncRange = SyncRange
            };
        }

        public void Dispose()
        {
            _lastSyncStates.Clear();
            _visibleRemoteNpcs.Clear();
        }
    }

    /// <summary>
    /// NPC 同步状态
    /// </summary>
    internal class NpcSyncState
    {
        public Vector3 Position { get; set; }
        public float RotationY { get; set; }
        public float LastSyncTime { get; set; }
    }

    /// <summary>
    /// 可见性统计信息
    /// </summary>
    public struct VisibilityStats
    {
        public int TrackedNpcs;
        public int VisibleRemoteNpcs;
        public float SyncRange;
    }
}

