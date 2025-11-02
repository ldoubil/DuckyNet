using System;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Managers;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 动画同步服务实现
    /// 接收客户端动画状态并广播给同场景其他玩家
    /// </summary>
    public class AnimatorSyncServiceImpl : IAnimatorSyncService
    {
        private readonly RpcServer _server;
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;

        public AnimatorSyncServiceImpl(RpcServer server, PlayerManager playerManager, RoomManager roomManager)
        {
            _server = server;
            _playerManager = playerManager;
            _roomManager = roomManager;
        }

        public void UpdateAnimatorState(IClientContext client, AnimatorSyncData animatorData)
        {
            try
            {
                // 步骤1: 获取发送者的玩家信息
                var senderPlayer = _playerManager.GetPlayer(client.ClientId);
                if (senderPlayer == null)
                {
                    Console.WriteLine($"[AnimatorSyncService] ⚠️ 无法找到客户端 {client.ClientId} 对应的玩家");
                    return;
                }

                // 步骤2: 验证发送者是否在房间中
                var room = _roomManager.GetPlayerRoom(senderPlayer);
                if (room == null)
                {
                    Console.WriteLine($"[AnimatorSyncService] 玩家 {senderPlayer.SteamName} 不在任何房间中，跳过同步");
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
                    
                    // 获取目标玩家的连接上下文
                    var targetClientId = _playerManager.GetClientIdBySteamId(targetPlayer.SteamId);
                    if (string.IsNullOrEmpty(targetClientId))
                    {
                        Console.WriteLine($"[AnimatorSyncService] ⚠️ 目标玩家 {targetPlayer.SteamName} 无 ClientId");
                        continue;
                    }

                    var targetClientContext = _server.GetClientContext(targetClientId);
                    if (targetClientContext != null)
                    {
                        try
                        {
                            // 通过 RPC 调用客户端接收动画状态
                            targetClientContext.Call<IAnimatorSyncClientService>()
                                .OnAnimatorStateUpdated(senderPlayer.SteamId, animatorData);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[AnimatorSyncService] ❌ 向 {targetPlayer.SteamName} 转发动画状态失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[AnimatorSyncService] ⚠️ 目标玩家 {targetPlayer.SteamName} 无上下文");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AnimatorSyncService] 错误: {ex.Message}");
                Console.WriteLine($"[AnimatorSyncService] 堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}
