using System;
using System.Collections.Generic;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Managers;
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
        private readonly RpcServer _server;
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;
        private readonly object _lock = new object();
        
        /// <summary>
        /// 玩家最后位置缓存 - Key: SteamId (string), Value: 最后的同步数据
        /// 用于新玩家加入房间时获取现有玩家的位置
        /// </summary>
        private readonly Dictionary<string, UnitySyncData> _lastPositionCache = new Dictionary<string, UnitySyncData>();

        public PlayerUnitySyncServiceImpl(RpcServer server, PlayerManager playerManager, RoomManager roomManager)
        {
            _server = server;
            _playerManager = playerManager;
            _roomManager = roomManager;
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
                var senderPlayer = _playerManager.GetPlayer(client.ClientId);
                if (senderPlayer == null)
                {
                    Console.WriteLine($"[PlayerUnitySyncService] ⚠️ 无法找到客户端 {client.ClientId} 对应的玩家");
                    return;
                }
                
                // 🔥 临时启用日志（用于调试）
                var (posX, posY, posZ) = syncData.GetPosition();
                Console.WriteLine($"[PlayerUnitySyncService] 🔥 收到 {senderPlayer.SteamName} 的位置同步: Pos({posX:F2},{posY:F2},{posZ:F2}) 场景:{senderPlayer.CurrentScenelData.SceneName}/{senderPlayer.CurrentScenelData.SubSceneName}");
                
                // 确保 SteamId 与发送者匹配（安全验证）
                syncData.SteamId = senderPlayer.SteamId;
                
                // 缓存最后位置（用于新玩家加入时同步）
                lock (_lock)
                {
                    _lastPositionCache[senderPlayer.SteamId] = syncData;
                }

                // 步骤2: 验证发送者是否在房间中
                var room = _roomManager.GetPlayerRoom(senderPlayer);
                if (room == null)
                {
                    Console.WriteLine($"[PlayerUnitySyncService] 玩家 {senderPlayer.SteamName} 不在任何房间中，跳过同步");
                    return;
                }

                // 步骤3: 获取房间内的所有玩家
                var roomPlayers = _roomManager.GetRoomPlayers(room.RoomId);

                // 步骤4: 广播给符合条件的玩家
                foreach (var targetPlayer in roomPlayers)
                {
                    // 条件1: 跳过发送者自己
                    if (targetPlayer.SteamId == senderPlayer.SteamId)
                    {
                        continue;
                    }

                    // 条件2: 检查是否在同一个房间（冗余检查，但保证安全）
                    var targetRoom = _roomManager.GetPlayerRoom(targetPlayer);
                    if (targetRoom == null || targetRoom.RoomId != room.RoomId)
                    {
                        continue;
                    }

                    // 条件3: 检查是否在同一个场景（SceneName）
                    if (targetPlayer.CurrentScenelData.SceneName != senderPlayer.CurrentScenelData.SceneName)
                    {
                        continue;
                    }

                    // 条件4: 检查是否在同一个子场景（SubSceneName）
                    if (targetPlayer.CurrentScenelData.SubSceneName != senderPlayer.CurrentScenelData.SubSceneName)
                    {
                        continue;
                    }

                    // 🔥 临时日志：显示满足条件的目标玩家
                    Console.WriteLine($"[PlayerUnitySyncService] 准备转发 {senderPlayer.SteamName} -> {targetPlayer.SteamName}");
                    
                    // 获取目标玩家的连接上下文
                    var targetClientId = _playerManager.GetClientIdBySteamId(targetPlayer.SteamId);
                    if (string.IsNullOrEmpty(targetClientId))
                    {
                        Console.WriteLine($"[PlayerUnitySyncService] ⚠️ 目标玩家 {targetPlayer.SteamName} 无 ClientId");
                        continue;
                    }

                    var targetClientContext = _server.GetClientContext(targetClientId);
                    if (targetClientContext != null)
                    {
                        try
                        {
                            // 通过 RPC 调用客户端接收同步数据
                            targetClientContext.Call<IPlayerClientService>().OnPlayerUnitySyncReceived(syncData);
                            
                            // 🔥 临时启用转发日志（用于调试）
                            Console.WriteLine($"[PlayerUnitySyncService] ✅ 已转发 {senderPlayer.SteamName} -> {targetPlayer.SteamName}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[PlayerUnitySyncService] ❌ 向 {targetPlayer.SteamName} 转发同步数据失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[PlayerUnitySyncService] ⚠️ 目标玩家 {targetPlayer.SteamName} 无上下文");
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayerUnitySyncService] 处理同步数据异常: {ex.Message}");
                Console.WriteLine($"[PlayerUnitySyncService] 堆栈跟踪: {ex.StackTrace}");
            }
        }

    }
}
