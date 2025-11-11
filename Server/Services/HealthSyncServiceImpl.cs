using System;
using System.Collections.Generic;
using DuckyNet.Server.Core;
using DuckyNet.Server.Events;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 血量同步服务实现
    /// 处理玩家血量同步数据的转发
    /// </summary>
    public class HealthSyncServiceImpl : IHealthSyncService
    {
        private readonly object _lock = new object();
        
        /// <summary>
        /// 玩家最后血量缓存 - Key: SteamId, Value: 最后的血量数据
        /// </summary>
        private readonly Dictionary<string, HealthSyncData> _lastHealthCache = new Dictionary<string, HealthSyncData>();

        public HealthSyncServiceImpl(EventBus eventBus)
        {
            // 订阅玩家断开事件，自动清理缓存
            eventBus.Subscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
        }

        /// <summary>
        /// 处理玩家断开事件：自动清理血量缓存
        /// </summary>
        private void OnPlayerDisconnected(PlayerDisconnectedEvent evt)
        {
            if (evt.Player != null)
            {
                ClearPlayerHealth(evt.Player.SteamId);
            }
        }
        
        /// <summary>
        /// 获取玩家的最后血量（用于新玩家加入房间时同步）
        /// </summary>
        public HealthSyncData? GetLastHealth(string steamId)
        {
            lock (_lock)
            {
                if (_lastHealthCache.TryGetValue(steamId, out var lastHealth))
                {
                    return lastHealth;
                }
                return null;
            }
        }
        
        /// <summary>
        /// 清除玩家的血量缓存（玩家离开房间/断开连接时调用）
        /// </summary>
        public void ClearPlayerHealth(string steamId)
        {
            lock (_lock)
            {
                if (_lastHealthCache.Remove(steamId))
                {
                    Console.WriteLine($"[HealthSyncService] 清除玩家 {steamId} 的血量缓存");
                }
            }
        }

        public void SendHealthSync(IClientContext client, HealthSyncData healthData)
        {
            try
            {
                // 获取发送者的玩家信息
                var senderPlayer = ServerContext.Players.GetPlayer(client.ClientId);
                if (senderPlayer == null)
                {
                    Console.WriteLine($"[HealthSyncService] ⚠️ 无法找到客户端 {client.ClientId} 对应的玩家");
                    return;
                }
                
                // 确保 SteamId 与发送者匹配（安全验证）
                healthData.SteamId = senderPlayer.SteamId;
                
                // 缓存最后血量（用于新玩家加入时同步）
                lock (_lock)
                {
                    _lastHealthCache[senderPlayer.SteamId] = healthData;
                }

                // 验证发送者是否在房间中
                var room = ServerContext.Rooms.GetPlayerRoom(senderPlayer);
                if (room == null)
                {
                    Console.WriteLine($"[HealthSyncService] 玩家 {senderPlayer.SteamName} 不在任何房间中，跳过同步");
                    return;
                }

                // 使用 BroadcastManager 广播给同场景的玩家
                ServerContext.Broadcast.BroadcastToScene(senderPlayer, (targetPlayer, targetContext) =>
                {
                    try
                    {
                        targetContext.Call<IHealthSyncClientService>().OnHealthSyncReceived(healthData);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[HealthSyncService] ❌ 向 {targetPlayer.SteamName} 转发血量数据失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HealthSyncService] 处理血量同步数据异常: {ex.Message}");
                Console.WriteLine($"[HealthSyncService] 堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}
