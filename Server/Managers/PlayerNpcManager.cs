using System;
using System.Collections.Generic;
using System.Linq;
using DuckyNet.Shared.Data;

namespace DuckyNet.Server.Managers
{
    /// <summary>
    /// 玩家 NPC 管理器 - 维护每个玩家拥有的 NPC 列表
    /// </summary>
    public class PlayerNpcManager
    {
        // 玩家 ID (SteamId) -> NPC 列表
        private readonly Dictionary<string, List<NpcSpawnData>> _playerNpcs = new Dictionary<string, List<NpcSpawnData>>();
        
        // NPC ID -> 拥有者玩家 ID (快速查找)
        private readonly Dictionary<string, string> _npcOwners = new Dictionary<string, string>();
        
        private readonly object _lock = new object();

        /// <summary>
        /// 添加玩家的 NPC
        /// </summary>
        public void AddNpc(string playerId, NpcSpawnData npcData)
        {
            lock (_lock)
            {
                if (!_playerNpcs.TryGetValue(playerId, out var npcs))
                {
                    npcs = new List<NpcSpawnData>();
                    _playerNpcs[playerId] = npcs;
                }

                // 去重
                if (npcs.Any(n => n.NpcId == npcData.NpcId))
                {
                    Console.WriteLine($"[PlayerNpcManager] NPC 已存在: {npcData.NpcId}");
                    return;
                }

                npcs.Add(npcData);
                _npcOwners[npcData.NpcId] = playerId;
                Console.WriteLine($"[PlayerNpcManager] 玩家 {playerId} 创建 NPC: {npcData.NpcId}");
            }
        }

        /// <summary>
        /// 移除玩家的 NPC
        /// </summary>
        public void RemoveNpc(string npcId)
        {
            lock (_lock)
            {
                if (_npcOwners.TryGetValue(npcId, out var playerId))
                {
                    if (_playerNpcs.TryGetValue(playerId, out var npcs))
                    {
                        npcs.RemoveAll(n => n.NpcId == npcId);
                    }
                    _npcOwners.Remove(npcId);
                    Console.WriteLine($"[PlayerNpcManager] 移除 NPC: {npcId}");
                }
            }
        }

        /// <summary>
        /// 更新 NPC 位置
        /// </summary>
        public void UpdateNpcPosition(string npcId, float x, float y, float z, float rotY)
        {
            lock (_lock)
            {
                if (_npcOwners.TryGetValue(npcId, out var playerId))
                {
                    if (_playerNpcs.TryGetValue(playerId, out var npcs))
                    {
                        var npc = npcs.FirstOrDefault(n => n.NpcId == npcId);
                        if (npc != null)
                        {
                            npc.PositionX = x;
                            npc.PositionY = y;
                            npc.PositionZ = z;
                            npc.RotationY = rotY;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取玩家的所有 NPC
        /// </summary>
        public List<NpcSpawnData> GetPlayerNpcs(string playerId)
        {
            lock (_lock)
            {
                if (_playerNpcs.TryGetValue(playerId, out var npcs))
                {
                    return new List<NpcSpawnData>(npcs);
                }
                return new List<NpcSpawnData>();
            }
        }

        /// <summary>
        /// 获取场景中所有玩家的 NPC（用于可见性计算）
        /// </summary>
        public List<NpcSpawnData> GetSceneNpcs(string sceneName, string subSceneName)
        {
            lock (_lock)
            {
                var result = new List<NpcSpawnData>();
                foreach (var npcs in _playerNpcs.Values)
                {
                    result.AddRange(npcs.Where(n => 
                        n.SceneName == sceneName && n.SubSceneName == subSceneName));
                }
                return result;
            }
        }

        /// <summary>
        /// 获取所有玩家 ID（同场景）
        /// </summary>
        public List<string> GetPlayersInScene(string sceneName, string subSceneName)
        {
            lock (_lock)
            {
                var result = new List<string>();
                foreach (var kvp in _playerNpcs)
                {
                    if (kvp.Value.Any(n => n.SceneName == sceneName && n.SubSceneName == subSceneName))
                    {
                        result.Add(kvp.Key);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// 清理玩家的所有 NPC（断开连接时）
        /// </summary>
        public void ClearPlayerNpcs(string playerId)
        {
            lock (_lock)
            {
                if (_playerNpcs.TryGetValue(playerId, out var npcs))
                {
                    // 清理反向索引
                    foreach (var npc in npcs)
                    {
                        _npcOwners.Remove(npc.NpcId);
                    }
                    _playerNpcs.Remove(playerId);
                    Console.WriteLine($"[PlayerNpcManager] 清理玩家 {playerId} 的 {npcs.Count} 个 NPC");
                }
            }
        }

        /// <summary>
        /// 获取 NPC 的拥有者
        /// </summary>
        public string? GetNpcOwner(string npcId)
        {
            lock (_lock)
            {
                return _npcOwners.TryGetValue(npcId, out var owner) ? owner : null;
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public (int TotalPlayers, int TotalNpcs) GetStats()
        {
            lock (_lock)
            {
                return (_playerNpcs.Count, _npcOwners.Count);
            }
        }
    }
}

