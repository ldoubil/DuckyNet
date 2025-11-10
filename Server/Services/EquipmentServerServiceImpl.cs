using DuckyNet.Shared.Data;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;
using DuckyNet.Server.Core;
using System;
using System.Threading.Tasks;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 装备同步服务器端实现
    /// 负责存储和广播玩家装备数据
    /// </summary>
    public class EquipmentServerServiceImpl : IEquipmentService
    {

        /// <summary>
        /// 客户端更新装备槽位
        /// </summary>
        public Task<bool> UpdateEquipmentSlotAsync(IClientContext client, EquipmentSlotUpdateRequest request)
        {
            if (client == null)
            {
                Log("UpdateEquipmentSlotAsync 失败：没有客户端上下文", ConsoleColor.Red);
                return Task.FromResult(false);
            }

            var playerId = client.ClientId;
            var player = ServerContext.Players.GetPlayer(playerId);
            
            if (player == null)
            {
                Log($"UpdateEquipmentSlotAsync 失败：找不到玩家 {playerId}", ConsoleColor.Red);
                return Task.FromResult(false);
            }

            try
            {
                // 更新服务器端的装备数据
                player.EquipmentData.SetEquipment(request.SlotType, request.ItemTypeId);

                string action = request.ItemTypeId.HasValue && request.ItemTypeId.Value > 0 ? "装备" : "卸下";
                Log($"玩家 {player.SteamName} {action}装备: {request.SlotType} = {request.ItemTypeId}", ConsoleColor.Green);

                // 广播给房间内的其他玩家
                BroadcastEquipmentUpdate(player, request);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log($"UpdateEquipmentSlotAsync 异常: {ex.Message}", ConsoleColor.Red);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 广播装备更新到房间内的其他玩家
        /// </summary>
        private void BroadcastEquipmentUpdate(PlayerInfo player, EquipmentSlotUpdateRequest request)
        {
            // 获取玩家所在的房间
            var room = ServerContext.Rooms.GetPlayerRoom(player);
            if (room == null)
            {
                Log($"玩家 {player.SteamName} 不在房间中，无需广播装备更新", ConsoleColor.Yellow);
                return;
            }

            // 创建广播通知
            var notification = new EquipmentSlotUpdateNotification
            {
                PlayerId = player.SteamId,
                SlotType = request.SlotType,
                ItemTypeId = request.ItemTypeId
            };

            // 使用 BroadcastManager 简化广播逻辑
            ServerContext.Broadcast.BroadcastToRoomTyped<IEquipmentClientService>(player, 
                service => service.OnEquipmentSlotUpdated(notification));
                
            Log($"装备更新已广播 (房间: {room.RoomId})", ConsoleColor.Cyan);
        }

        /// <summary>
        /// 当玩家加入房间时，发送所有玩家的装备数据
        /// 由 RoomService 调用
        /// </summary>
        public void SendAllEquipmentDataToPlayer(string clientId, string roomId)
        {
            try
            {
                var roomPlayers = ServerContext.Rooms.GetRoomPlayers(roomId);
                if (roomPlayers == null || roomPlayers.Length == 0)
                {
                    Log($"房间 {roomId} 没有其他玩家，跳过发送装备数据", ConsoleColor.Yellow);
                    return;
                }

                // 收集所有玩家的装备数据
                var allEquipmentData = new AllPlayersEquipmentData();
                
                foreach (var player in roomPlayers)
                {
                    // 包括自己的数据也发送（客户端可以用来验证）
                    if (player.EquipmentData.GetEquippedCount() > 0)
                    {
                        allEquipmentData.PlayersEquipment[player.SteamId] = player.EquipmentData.Clone();
                    }
                }

                // 发送给新加入的玩家
                var clientContext = ServerContext.Server.GetClientContext(clientId);
                if (clientContext != null)
                {
                    clientContext.Call<IEquipmentClientService>()
                        .OnAllPlayersEquipmentReceived(allEquipmentData);
                    
                    Log($"已向玩家发送房间装备数据: {allEquipmentData.PlayersEquipment.Count} 个玩家", ConsoleColor.Green);
                }
            }
            catch (Exception ex)
            {
                Log($"SendAllEquipmentDataToPlayer 失败: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// 日志输出
        /// </summary>
        private void Log(string message, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[EquipmentService] {message}");
            Console.ResetColor();
        }
    }
}

