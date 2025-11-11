using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 影子 NPC 对象池
    /// 
    /// 功能：
    /// 1. 复用 GameObject，避免频繁创建/销毁
    /// 2. 减少 GC 压力
    /// 3. 提高性能
    /// 
    /// 策略：
    /// - 按 NPC 类型分池（不同类型不共用）
    /// - 预热：启动时预创建常用 NPC
    /// - 动态扩容：不够时自动创建新的
    /// - 自动回收：长时间未使用的对象销毁
    /// </summary>
    public class ShadowNpcPool : IDisposable
    {
        // 按 NPC 类型分池
        private readonly Dictionary<string, Queue<PooledNpc>> _pools = new Dictionary<string, Queue<PooledNpc>>();
        
        // 正在使用的 NPC（用于追踪）
        private readonly Dictionary<string, PooledNpc> _activeNpcs = new Dictionary<string, PooledNpc>();

        // 配置
        public int DefaultPoolSize { get; set; } = 10; // 每个类型的默认池大小
        public int MaxPoolSize { get; set; } = 50; // 每个类型的最大池大小
        public float AutoRecycleTime { get; set; } = 60f; // 60秒未使用自动回收

        // 统计
        private int _totalCreated = 0;
        private int _totalReused = 0;
        private int _totalRecycled = 0;

        /// <summary>
        /// 预热对象池（场景加载时调用）
        /// </summary>
        public void WarmUp(string npcType, int count)
        {
            if (!_pools.ContainsKey(npcType))
            {
                _pools[npcType] = new Queue<PooledNpc>();
            }

            for (int i = 0; i < count; i++)
            {
                var npcData = new NpcSpawnData
                {
                    NpcId = Guid.NewGuid().ToString(),
                    NpcType = npcType,
                    PositionX = 0,
                    PositionY = -1000, // 放到地下
                    PositionZ = 0,
                    RotationY = 0
                };

                var npc = CreateNewNpc(npcData);
                if (npc != null && npc.GameObject != null)
                {
                    npc.GameObject.SetActive(false); // 禁用
                    _pools[npcType].Enqueue(npc);
                }
            }

            Debug.Log($"[ShadowNpcPool] 预热完成: {npcType} × {count}");
        }

        /// <summary>
        /// 从池中获取或创建 NPC
        /// </summary>
        public (object? characterMainControl, GameObject? gameObject) Get(NpcSpawnData data)
        {
            var npcType = data.NpcType;

            if (!_pools.ContainsKey(npcType))
            {
                _pools[npcType] = new Queue<PooledNpc>();
            }

            PooledNpc? pooledNpc = null;

            // 尝试从池中获取
            if (_pools[npcType].Count > 0)
            {
                pooledNpc = _pools[npcType].Dequeue();
                _totalReused++;
                Debug.Log($"[ShadowNpcPool] ♻️ 复用 NPC: {npcType} (池剩余: {_pools[npcType].Count})");
            }
            else
            {
                // 池为空，创建新的
                pooledNpc = CreateNewNpc(data);
                _totalCreated++;
                Debug.Log($"[ShadowNpcPool] 🆕 创建新 NPC: {npcType} (总创建: {_totalCreated})");
            }

            if (pooledNpc == null)
                return (null, null);

            // 重置状态
            ResetNpc(pooledNpc, data);

            // 激活并记录
            if (pooledNpc.GameObject != null)
            {
                pooledNpc.GameObject.SetActive(true);
            }
            pooledNpc.LastUsedTime = Time.time;
            _activeNpcs[data.NpcId] = pooledNpc;

            return (pooledNpc.CharacterMainControl, pooledNpc.GameObject);
        }

        /// <summary>
        /// 回收 NPC 到池
        /// </summary>
        public void Recycle(string npcId)
        {
            if (!_activeNpcs.TryGetValue(npcId, out var pooledNpc))
            {
                Debug.LogWarning($"[ShadowNpcPool] NPC 不在活动列表中: {npcId}");
                return;
            }

            _activeNpcs.Remove(npcId);

            var npcType = pooledNpc.NpcType;
            if (!_pools.ContainsKey(npcType))
            {
                _pools[npcType] = new Queue<PooledNpc>();
            }

            // 检查池是否已满
            if (_pools[npcType].Count >= MaxPoolSize)
            {
                // 池满了，直接销毁
                if (pooledNpc.GameObject != null)
                {
                    UnityEngine.Object.Destroy(pooledNpc.GameObject);
                }
                Debug.Log($"[ShadowNpcPool] 池已满，销毁 NPC: {npcType}");
                return;
            }

            // 禁用并回收
            if (pooledNpc.GameObject != null)
            {
                pooledNpc.GameObject.SetActive(false);
                pooledNpc.GameObject.transform.position = new Vector3(0, -1000, 0); // 移到地下
            }

            pooledNpc.LastUsedTime = Time.time;
            _pools[npcType].Enqueue(pooledNpc);
            _totalRecycled++;

            Debug.Log($"[ShadowNpcPool] ♻️ 回收 NPC: {npcType} (池数量: {_pools[npcType].Count})");
        }

        /// <summary>
        /// 创建新的 NPC
        /// </summary>
        private PooledNpc? CreateNewNpc(NpcSpawnData data)
        {
            var character = ShadowNpcFactory.CreateShadowNpc(data);
            if (character == null) return null;

            GameObject? gameObject = (character is Component comp) ? comp.gameObject : null;
            if (gameObject == null) return null;

            return new PooledNpc
            {
                NpcType = data.NpcType,
                CharacterMainControl = character,
                GameObject = gameObject,
                CreatedTime = Time.time,
                LastUsedTime = Time.time
            };
        }

        /// <summary>
        /// 重置 NPC 状态
        /// </summary>
        private void ResetNpc(PooledNpc npc, NpcSpawnData data)
        {
            if (npc.GameObject == null) return;

            // 重置位置和旋转
            npc.GameObject.transform.position = new Vector3(data.PositionX, data.PositionY, data.PositionZ);
            npc.GameObject.transform.rotation = Quaternion.Euler(0, data.RotationY, 0);

            // 重置名称
            npc.GameObject.name = $"RemoteNPC_{data.NpcType}";

            // 更新标记组件
            var marker = npc.GameObject.GetComponent<ShadowNpcMarker>();
            if (marker != null)
            {
                marker.NpcId = data.NpcId;
                marker.NpcType = data.NpcType;
                marker.SceneName = data.SceneName;
                marker.SubSceneName = data.SubSceneName;
            }
        }

        /// <summary>
        /// 清理长时间未使用的 NPC（定期调用，如每分钟）
        /// </summary>
        public void CleanupUnused()
        {
            int cleaned = 0;
            foreach (var kvp in _pools)
            {
                var npcType = kvp.Key;
                var pool = kvp.Value;

                // 临时列表
                var toKeep = new Queue<PooledNpc>();

                while (pool.Count > 0)
                {
                    var npc = pool.Dequeue();
                    
                    // 检查是否超时
                    if (Time.time - npc.LastUsedTime > AutoRecycleTime)
                    {
                        // 销毁
                        if (npc.GameObject != null)
                        {
                            UnityEngine.Object.Destroy(npc.GameObject);
                        }
                        cleaned++;
                    }
                    else
                    {
                        // 保留
                        toKeep.Enqueue(npc);
                    }
                }

                // 重建队列
                _pools[npcType] = toKeep;
            }

            if (cleaned > 0)
            {
                Debug.Log($"[ShadowNpcPool] 🧹 清理未使用的 NPC: {cleaned} 个");
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public PoolStats GetStats()
        {
            int totalPooled = 0;
            foreach (var pool in _pools.Values)
            {
                totalPooled += pool.Count;
            }

            return new PoolStats
            {
                TotalCreated = _totalCreated,
                TotalReused = _totalReused,
                TotalRecycled = _totalRecycled,
                ActiveNpcs = _activeNpcs.Count,
                PooledNpcs = totalPooled,
                PoolTypes = _pools.Count,
                ReuseRate = _totalCreated > 0 ? (_totalReused / (float)(_totalCreated + _totalReused)) * 100f : 0f
            };
        }

        /// <summary>
        /// 清理所有池
        /// </summary>
        public void Dispose()
        {
            // 销毁所有活动 NPC
            foreach (var npc in _activeNpcs.Values)
            {
                if (npc.GameObject != null)
                {
                    UnityEngine.Object.Destroy(npc.GameObject);
                }
            }
            _activeNpcs.Clear();

            // 销毁所有池中的 NPC
            foreach (var pool in _pools.Values)
            {
                while (pool.Count > 0)
                {
                    var npc = pool.Dequeue();
                    if (npc.GameObject != null)
                    {
                        UnityEngine.Object.Destroy(npc.GameObject);
                    }
                }
            }
            _pools.Clear();

            Debug.Log($"[ShadowNpcPool] 对象池已清理（复用率: {GetStats().ReuseRate:F1}%）");
        }
    }

    /// <summary>
    /// 池化的 NPC
    /// </summary>
    internal class PooledNpc
    {
        public string NpcType { get; set; } = "";
        public object? CharacterMainControl { get; set; }
        public GameObject? GameObject { get; set; }
        public float CreatedTime { get; set; }
        public float LastUsedTime { get; set; }
    }

    /// <summary>
    /// 对象池统计
    /// </summary>
    public struct PoolStats
    {
        public int TotalCreated;      // 总创建数
        public int TotalReused;       // 总复用数
        public int TotalRecycled;     // 总回收数
        public int ActiveNpcs;        // 当前活动数
        public int PooledNpcs;        // 当前池中数
        public int PoolTypes;         // 池类型数
        public float ReuseRate;       // 复用率（%）
    }
}

