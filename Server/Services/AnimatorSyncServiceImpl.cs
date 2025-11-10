using System;
using DuckyNet.Server.Core;
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
        public void UpdateAnimatorState(IClientContext client, AnimatorSyncData animatorData)
        {
            try
            {
                // 获取发送者的玩家信息
                var senderPlayer = ServerContext.Players.GetPlayer(client.ClientId);
                if (senderPlayer == null)
                {
                    Console.WriteLine($"[AnimatorSyncService] ⚠️ 无法找到客户端 {client.ClientId} 对应的玩家");
                    return;
                }

                // 验证发送者是否在房间中
                var room = ServerContext.Rooms.GetPlayerRoom(senderPlayer);
                if (room == null)
                {
                    Console.WriteLine($"[AnimatorSyncService] 玩家 {senderPlayer.SteamName} 不在任何房间中，跳过同步");
                    return;
                }

                // 使用 BroadcastManager 广播给同场景的玩家
                ServerContext.Broadcast.BroadcastToScene(senderPlayer, (targetPlayer, targetContext) =>
                {
                    try
                    {
                        targetContext.Call<IAnimatorSyncClientService>()
                            .OnAnimatorStateUpdated(senderPlayer.SteamId, animatorData);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AnimatorSyncService] ❌ 向 {targetPlayer.SteamName} 转发动画状态失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AnimatorSyncService] 错误: {ex.Message}");
                Console.WriteLine($"[AnimatorSyncService] 堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}
