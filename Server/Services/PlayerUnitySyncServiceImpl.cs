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
    /// 玩家Unity同步服务实现
    /// 处理玩家位置、旋转、速度等实时同步数据
    /// </summary>
    public class PlayerUnitySyncServiceImpl : IPlayerUnitySyncService
    {
        private readonly object _lock = new object();
        
        /// <summary>
        /// 玩家最后位置缓存 - Key: SteamId (string), Value: 最后的同步数据
        /// 用于新玩家加入房间时获取现有玩家的位置
        /// </summary>
        private readonly Dictionary<string, UnitySyncData> _lastPositionCache = new Dictionary<string, UnitySyncData>();

        public PlayerUnitySyncServiceImpl(EventBus eventBus)
        {
            // 订阅玩家断开事件，自动清理缓存
            eventBus.Subscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
        }

        /// <summary>
        /// 处理玩家断开事件：自动清理位置缓存
        /// </summary>
        private void OnPlayerDisconnected(PlayerDisconnectedEvent evt)
        {
            if (evt.Player != null)
            {
                ClearPlayerPosition(evt.Player.SteamId);
            }
        }
        
        /// <summary>
        /// 获取玩家的最后位置（用于新玩家加入房间时同步）
        /// </summary>
        public UnitySyncData? GetLastPosition(string steamId)
        {
            lock (_lock)
            {
                if (_lastPositionCache.TryGetValue(steamId, out var lastSync))
                {
                    return lastSync;
                }
                return null;
            }
        }
        
        /// <summary>
        /// 清除玩家的位置缓存（玩家离开房间/断开连接时调用）
        /// </summary>
        public void ClearPlayerPosition(string steamId)
        {
            lock (_lock)
            {
                if (_lastPositionCache.Remove(steamId))
                {
                    Console.WriteLine($"[PlayerUnitySyncService] 清除玩家 {steamId} 的位置缓存");
                }
            }
        }

        public void SendPlayerUnitySync(IClientContext client, UnitySyncData syncData)
        {
            try
            {
                // 步骤1: 获取发送者的玩家信息
                var senderPlayer = ServerContext.Players.GetPlayer(client.ClientId);
                if (senderPlayer == null)
                {
                    Console.WriteLine($"[PlayerUnitySyncService] ⚠️ 无法找到客户端 {client.ClientId} 对应的玩家");
                    return;
                }

                // 🔥 缓存玩家位置到 SceneManager（用于热区和范围计算）
                var (x, y, z) = syncData.GetPosition();
                ServerContext.Scenes.UpdatePlayerPosition(
                    senderPlayer.SteamId,
                    x,
                    y,
                    z
                );
                
                // 确保 SteamId 与发送者匹配（安全验证）
                syncData.SteamId = senderPlayer.SteamId;
                
                // 缓存最后位置（用于新玩家加入时同步）
                lock (_lock)
                {
                    _lastPositionCache[senderPlayer.SteamId] = syncData;
                }

                // 步骤2: 验证发送者是否在房间中
                var room = ServerContext.Rooms.GetPlayerRoom(senderPlayer);
                if (room == null)
                {
                    Console.WriteLine($"[PlayerUnitySyncService] 玩家 {senderPlayer.SteamName} 不在任何房间中，跳过同步");
                    return;
                }

                // 步骤3: 使用 BroadcastManager 广播给同场景的玩家
                ServerContext.Broadcast.BroadcastToScene(senderPlayer, (targetPlayer, targetContext) =>
                {
                    try
                    {
                        // 通过 RPC 调用客户端接收同步数据
                        targetContext.Call<IPlayerClientService>().OnPlayerUnitySyncReceived(syncData);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PlayerUnitySyncService] ❌ 向 {targetPlayer.SteamName} 转发同步数据失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayerUnitySyncService] 处理同步数据异常: {ex.Message}");
                Console.WriteLine($"[PlayerUnitySyncService] 堆栈跟踪: {ex.StackTrace}");
            }
        }

    }
}
