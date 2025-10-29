using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;
using DuckyNet.Server.RPC;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 角色同步服务实现（服务器端）
    /// 负责接收客户端同步数据并广播给其他客户端
    /// </summary>
    public class CharacterSyncServiceImpl : ICharacterSyncService
    {
        private readonly RpcServer _rpcServer;
        private readonly Managers.PlayerManager _playerManager;
        private readonly Managers.RoomManager _roomManager;
        
        // 存储所有玩家的最新同步数据
        private readonly ConcurrentDictionary<string, CharacterSyncData> _characterStates 
            = new ConcurrentDictionary<string, CharacterSyncData>();

        // 场景内的玩家映射：SceneId -> HashSet<PlayerId>
        private readonly ConcurrentDictionary<string, ConcurrentHashSet<string>> _scenePlayers 
            = new ConcurrentDictionary<string, ConcurrentHashSet<string>>();

        public CharacterSyncServiceImpl(RpcServer rpcServer, Managers.PlayerManager playerManager, Managers.RoomManager roomManager)
        {
            _rpcServer = rpcServer ?? throw new ArgumentNullException(nameof(rpcServer));
            _playerManager = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
            _roomManager = roomManager ?? throw new ArgumentNullException(nameof(roomManager));
            Console.WriteLine("[CharacterSyncService] 服务已创建");
        }

        // 线程安全的 HashSet
        private class ConcurrentHashSet<T>
        {
            private readonly HashSet<T> _set = new HashSet<T>();
            private readonly object _lock = new object();

            public void Add(T item)
            {
                lock (_lock) { _set.Add(item); }
            }

            public bool Remove(T item)
            {
                lock (_lock) { return _set.Remove(item); }
            }

            public HashSet<T> GetSnapshot()
            {
                lock (_lock) { return new HashSet<T>(_set); }
            }

            public int Count
            {
                get { lock (_lock) { return _set.Count; } }
            }
        }

        /// <summary>
        /// 接收客户端的角色同步数据
        /// </summary>
        public async Task SyncCharacterState(IClientContext client, CharacterSyncData syncData)
        {
            try
            {
                if (syncData == null || string.IsNullOrEmpty(syncData.PlayerId))
                {
                    Console.WriteLine("[CharacterSyncService] ⚠️ 无效的同步数据");
                    return;
                }

                // 从玩家信息获取房间ID（作为场景ID）
                var player = _playerManager.GetPlayer(client.ClientId);
                if (player != null)
                {
                    // 尝试获取玩家所在的房间
                    var room = _roomManager.GetPlayerRoom(player.SteamId);
                    if (room != null)
                    {
                        // 使用房间ID作为场景ID
                        syncData.SceneId = room.RoomId;
                    }
                    // 如果不在房间中，SceneId 留空，表示不在任何场景
                }

                // 更新服务器缓存
                _characterStates[syncData.PlayerId] = syncData;

                // 更新场景玩家映射
                if (!string.IsNullOrEmpty(syncData.SceneId))
                {
                    var sceneSet = _scenePlayers.GetOrAdd(syncData.SceneId, _ => new ConcurrentHashSet<string>());
                    sceneSet.Add(syncData.PlayerId);
                }

                // 只广播给同一场景的其他玩家
                await BroadcastToScene(syncData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CharacterSyncService] 同步失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 广播给同一场景的玩家
        /// </summary>
        private async Task BroadcastToScene(CharacterSyncData syncData)
        {
            try
            {
                // 如果没有场景ID，不广播
                if (string.IsNullOrEmpty(syncData.SceneId))
                {
                    return;
                }

                // 获取同一场景的所有玩家
                if (_scenePlayers.TryGetValue(syncData.SceneId, out var scenePlayers))
                {
                    var playerList = scenePlayers.GetSnapshot();
                    var targetCount = 0;

                    foreach (var playerId in playerList)
                    {
                        // 跳过发送者自己
                        if (playerId == syncData.PlayerId)
                        {
                            continue;
                        }

                        // 使用 SteamId 获取 ClientContext
                        var clientContext = _rpcServer.GetClientContext(playerId);
                        if (clientContext != null)
                        {
                            // 使用生成的强类型代理
                            clientContext.Call<ICharacterSyncClientService>().OnCharacterStateUpdate(syncData);
                            targetCount++;
                        }
                    }

                    if (targetCount > 0)
                    {
                        Console.WriteLine($"[CharacterSyncService] 同步 {syncData.PlayerId} 到场景 {syncData.SceneId} 的 {targetCount} 个玩家");
                    }
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CharacterSyncService] 场景广播失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 客户端请求完整场景状态（只返回同一场景的玩家）
        /// </summary>
        public async Task RequestFullState(IClientContext client)
        {
            try
            {
                var player = _playerManager.GetPlayer(client.ClientId);
                if (player == null)
                {
                    Console.WriteLine($"[CharacterSyncService] 玩家未找到: {client.ClientId}");
                    return;
                }

                // 确定请求者所在的场景
                var room = _roomManager.GetPlayerRoom(player.SteamId);
                var sceneId = room?.RoomId;

                // 如果不在房间中，没有场景，直接返回
                if (string.IsNullOrEmpty(sceneId))
                {
                    Console.WriteLine($"[CharacterSyncService] 玩家 {player.SteamName} 不在任何场景中");
                    return;
                }

                Console.WriteLine($"[CharacterSyncService] 玩家 {player.SteamName} 请求场景 {sceneId} 的完整状态");

                // 获取同一场景的其他玩家状态
                var sceneStates = _characterStates.Values
                    .Where(s => s.SceneId == sceneId && s.PlayerId != player.SteamId)
                    .ToArray();

                if (sceneStates.Length > 0)
                {
                    // 只发送同一场景的玩家状态
                    // 使用生成的强类型代理
                    client.Call<ICharacterSyncClientService>().OnFullStateUpdate(sceneStates);
                    
                    Console.WriteLine($"[CharacterSyncService] 已发送场景 {sceneId} 的 {sceneStates.Length} 个角色状态给 {player.SteamName}");
                }
                else
                {
                    Console.WriteLine($"[CharacterSyncService] 场景 {sceneId} 没有其他玩家");
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CharacterSyncService] 请求完整状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 玩家离开时清理
        /// </summary>
        public async Task OnPlayerDisconnected(string playerId)
        {
            try
            {
                if (_characterStates.TryRemove(playerId, out var syncData))
                {
                    Console.WriteLine($"[CharacterSyncService] 移除玩家状态: {playerId}");

                    // 从场景玩家映射中移除
                    if (!string.IsNullOrEmpty(syncData.SceneId))
                    {
                        if (_scenePlayers.TryGetValue(syncData.SceneId, out var sceneSet))
                        {
                            sceneSet.Remove(playerId);
                            
                            // 如果场景为空，清理映射
                            if (sceneSet.Count == 0)
                            {
                                _scenePlayers.TryRemove(syncData.SceneId, out _);
                            }
                        }
                    }

                    // 只通知同一场景的其他客户端
                    if (!string.IsNullOrEmpty(syncData.SceneId) && _scenePlayers.TryGetValue(syncData.SceneId, out var scenePlayers))
                    {
                        foreach (var otherPlayerId in scenePlayers.GetSnapshot())
                        {
                            var clientContext = _rpcServer.GetClientContext(otherPlayerId);
                            if (clientContext != null)
                            {
                                // 使用生成的强类型代理
                                clientContext.Call<ICharacterSyncClientService>().OnCharacterLeft(playerId);
                            }
                        }
                    }
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CharacterSyncService] 处理玩家离开失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 玩家切换场景时更新
        /// </summary>
        public void OnPlayerSceneChanged(string playerId, string oldSceneId, string newSceneId)
        {
            try
            {
                // 从旧场景移除
                if (!string.IsNullOrEmpty(oldSceneId) && _scenePlayers.TryGetValue(oldSceneId, out var oldSceneSet))
                {
                    oldSceneSet.Remove(playerId);
                    
                    // 通知旧场景的玩家该角色离开
                    foreach (var otherPlayerId in oldSceneSet.GetSnapshot())
                    {
                        var clientContext = _rpcServer.GetClientContext(otherPlayerId);
                        if (clientContext != null)
                        {
                            // 使用生成的强类型代理
                            clientContext.Call<ICharacterSyncClientService>().OnCharacterLeft(playerId);
                        }
                    }
                }

                // 添加到新场景
                if (!string.IsNullOrEmpty(newSceneId))
                {
                    var newSceneSet = _scenePlayers.GetOrAdd(newSceneId, _ => new ConcurrentHashSet<string>());
                    newSceneSet.Add(playerId);

                    // 更新角色的场景ID
                    if (_characterStates.TryGetValue(playerId, out var syncData))
                    {
                        syncData.SceneId = newSceneId;
                    }
                }

                Console.WriteLine($"[CharacterSyncService] 玩家 {playerId} 从场景 {oldSceneId} 切换到 {newSceneId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CharacterSyncService] 场景切换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前所有玩家状态（用于调试）
        /// </summary>
        public IReadOnlyDictionary<string, CharacterSyncData> GetAllStates()
        {
            return _characterStates;
        }
    }
}

