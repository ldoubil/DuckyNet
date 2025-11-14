using DuckyNet.Shared.Data;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Services;
using DuckyNet.Server.Core;
using System;
using System.Threading.Tasks;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 武器同步服务器端实现
    /// 负责存储和广播玩家武器数据
    /// </summary>
    public class WeaponSyncServerServiceImpl : IWeaponSyncService
    {

        /// <summary>
        /// 装备武器到槽位
        /// </summary>
        public Task<bool> EquipWeaponAsync(IClientContext client, WeaponSlotUpdateRequest request)
        {
            if (client == null)
            {
                Log("EquipWeaponAsync 失败：没有客户端上下文", ConsoleColor.Red);
                return Task.FromResult(false);
            }

            var playerId = client.ClientId;
            var player = ServerContext.Players.GetPlayer(playerId);

            if (player == null)
            {
                Log($"EquipWeaponAsync 失败：找不到玩家 {playerId}", ConsoleColor.Red);
                return Task.FromResult(false);
            }

            try
            {
                // 创建武器数据
                var weaponData = new WeaponItemData
                {
                    ItemTypeId = request.ItemTypeId,
                    ItemName = request.ItemName,
                    IsDefaultItem = request.IsDefaultItem,
                    ItemDataCompressed = request.ItemDataCompressed
                };

                // 更新服务器端的武器数据
                if (player.WeaponData == null)
                {
                    player.WeaponData = new PlayerWeaponData();
                }

                player.WeaponData.SetWeapon(request.SlotType, weaponData);

                string dataSize = request.IsDefaultItem ? "默认" : $"{request.ItemDataCompressed.Length}字节";
                Log($"玩家 {player.SteamName} 装备武器: {request.SlotType} = {request.ItemName} (TypeID={request.ItemTypeId}, 数据={dataSize})", 
                    ConsoleColor.Green);

                // 广播给房间内的其他玩家
                BroadcastWeaponUpdate(player, request, isUnequip: false);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log($"EquipWeaponAsync 异常: {ex.Message}", ConsoleColor.Red);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 卸下武器槽位
        /// </summary>
        public Task<bool> UnequipWeaponAsync(IClientContext client, WeaponSlotUnequipRequest request)
        {
            if (client == null)
            {
                Log("UnequipWeaponAsync 失败：没有客户端上下文", ConsoleColor.Red);
                return Task.FromResult(false);
            }

            var playerId = client.ClientId;
            var player = ServerContext.Players.GetPlayer(playerId);

            if (player == null)
            {
                Log($"UnequipWeaponAsync 失败：找不到玩家 {playerId}", ConsoleColor.Red);
                return Task.FromResult(false);
            }

            try
            {
                // 更新服务器端的武器数据（设为null）
                if (player.WeaponData == null)
                {
                    player.WeaponData = new PlayerWeaponData();
                }

                player.WeaponData.SetWeapon(request.SlotType, null);

                Log($"玩家 {player.SteamName} 卸下武器: {request.SlotType}", ConsoleColor.Yellow);

                // 创建卸下通知
                var notification = new WeaponSlotUpdateNotification
                {
                    PlayerId = player.SteamId,
                    SlotType = request.SlotType,
                    ItemTypeId = 0, // 0 表示卸下
                    ItemName = "",
                    IsDefaultItem = true,
                    ItemDataCompressed = ""
                };

                // 广播给房间内的其他玩家
                BroadcastWeaponNotification(player, notification);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log($"UnequipWeaponAsync 异常: {ex.Message}", ConsoleColor.Red);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 广播武器射击特效
        /// </summary>
        public Task BroadcastWeaponFireAsync(IClientContext client, WeaponFireData fireData)
        {
            if (client == null)
            {
                Log("BroadcastWeaponFireAsync 失败：没有客户端上下文", ConsoleColor.Red);
                return Task.CompletedTask;
            }

            var playerId = client.ClientId;
            var player = ServerContext.Players.GetPlayer(playerId);

            if (player == null)
            {
                Log($"BroadcastWeaponFireAsync 失败：找不到玩家 {playerId}", ConsoleColor.Red);
                return Task.CompletedTask;
            }

            try
            {
                // 设置玩家ID
                fireData.PlayerId = player.SteamId;

                // 广播给房间内的其他玩家
                BroadcastWeaponFireToRoom(player, fireData);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log($"BroadcastWeaponFireAsync 异常: {ex.Message}", ConsoleColor.Red);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 切换当前武器槽位
        /// </summary>
        public Task<bool> SwitchWeaponSlotAsync(IClientContext client, WeaponSwitchRequest request)
        {
            if (client == null)
            {
                Log("SwitchWeaponSlotAsync 失败：没有客户端上下文", ConsoleColor.Red);
                return Task.FromResult(false);
            }

            var playerId = client.ClientId;
            var player = ServerContext.Players.GetPlayer(playerId);

            if (player == null)
            {
                Log($"SwitchWeaponSlotAsync 失败：找不到玩家 {playerId}", ConsoleColor.Red);
                return Task.FromResult(false);
            }

            try
            {
                // 更新服务器端的当前武器槽位
                if (player.WeaponData == null)
                {
                    player.WeaponData = new PlayerWeaponData();
                }

                player.WeaponData.CurrentWeaponSlot = request.CurrentWeaponSlot;

                Log($"玩家 {player.SteamName} 切换武器: {request.CurrentWeaponSlot}", ConsoleColor.Cyan);

                // 创建切换通知
                var notification = new WeaponSwitchNotification
                {
                    PlayerId = player.SteamId,
                    CurrentWeaponSlot = request.CurrentWeaponSlot
                };

                // 广播给房间内的其他玩家
                BroadcastWeaponSwitchNotification(player, notification);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log($"SwitchWeaponSlotAsync 异常: {ex.Message}", ConsoleColor.Red);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 广播武器更新到房间内的其他玩家
        /// </summary>
        private void BroadcastWeaponUpdate(PlayerInfo player, WeaponSlotUpdateRequest request, bool isUnequip)
        {
            var notification = new WeaponSlotUpdateNotification
            {
                PlayerId = player.SteamId,
                SlotType = request.SlotType,
                ItemTypeId = isUnequip ? 0 : request.ItemTypeId,
                ItemName = request.ItemName,
                IsDefaultItem = request.IsDefaultItem,
                ItemDataCompressed = request.ItemDataCompressed
            };

            BroadcastWeaponNotification(player, notification);
        }

        /// <summary>
        /// 广播武器通知（只发送给同房间且同场景的玩家）
        /// </summary>
        private void BroadcastWeaponNotification(PlayerInfo player, WeaponSlotUpdateNotification notification)
        {
            // 使用 BroadcastManager 简化广播逻辑
            var room = ServerContext.Rooms.GetPlayerRoom(player);
            if (room == null)
            {
                Log($"玩家 {player.SteamName} 不在房间中，无需广播武器更新", ConsoleColor.Yellow);
                return;
            }

            ServerContext.Broadcast.BroadcastToSceneTyped<IWeaponSyncClientService>(player, 
                service => service.OnWeaponSlotUpdated(notification));

            Log($"武器更新已广播 (房间: {room.RoomId}, 场景: {player.CurrentScenelData.SceneName})", ConsoleColor.Cyan);
        }

        /// <summary>
        /// 批量通知武器开火（播放特效）- 霰弹枪/连发武器优化
        /// 🚀 性能优化：霰弹枪 8 发弹丸只需 1 次 RPC 调用
        /// </summary>
        public void NotifyWeaponFireBatch(IClientContext client, WeaponFireBatchData batchData)
        {
            if (client == null || batchData == null || batchData.BulletCount == 0)
            {
                Log("NotifyWeaponFireBatch 失败：无效参数", ConsoleColor.Red);
                return;
            }

            var playerId = client.ClientId;
            var player = ServerContext.Players.GetPlayer(playerId);

            if (player == null)
            {
                Log($"NotifyWeaponFireBatch 失败：找不到玩家 {playerId}", ConsoleColor.Red);
                return;
            }

            try
            {
                // 设置 PlayerId
                batchData.PlayerId = player.SteamId;

                // 🔥 批量广播给房间内的其他玩家
                BroadcastWeaponFireBatchToRoom(player, batchData);
            }
            catch (Exception ex)
            {
                Log($"NotifyWeaponFireBatch 异常: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// 通知武器开火（播放特效）- 单发
        /// </summary>
        public void NotifyWeaponFire(IClientContext client, WeaponFireData fireData)
        {
            if (client == null)
            {
                Log("NotifyWeaponFire 失败：没有客户端上下文", ConsoleColor.Red);
                return;
            }

            var playerId = client.ClientId;
            var player = ServerContext.Players.GetPlayer(playerId);

            if (player == null)
            {
                Log($"NotifyWeaponFire 失败：找不到玩家 {playerId}", ConsoleColor.Red);
                return;
            }

            try
            {
                // 设置 PlayerId
                fireData.PlayerId = player.SteamId;

                // 广播给房间内的其他玩家
                BroadcastWeaponFireNotification(player, fireData);
            }
            catch (Exception ex)
            {
                Log($"NotifyWeaponFire 异常: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// 广播武器开火通知（只发送给同房间且同场景的玩家）
        /// </summary>
        private void BroadcastWeaponFireNotification(PlayerInfo player, WeaponFireData fireData)
        {
            // 使用 BroadcastManager 简化广播逻辑
            ServerContext.Broadcast.BroadcastToSceneTyped<IWeaponSyncClientService>(player, 
                service => service.OnWeaponFired(fireData));
        }

        /// <summary>
        /// 广播武器切换通知（只发送给同房间且同场景的玩家）
        /// </summary>
        private void BroadcastWeaponSwitchNotification(PlayerInfo player, WeaponSwitchNotification notification)
        {
            var room = ServerContext.Rooms.GetPlayerRoom(player);
            if (room == null)
            {
                Log($"玩家 {player.SteamName} 不在房间中，无需广播武器切换", ConsoleColor.Yellow);
                return;
            }

            ServerContext.Broadcast.BroadcastToSceneTyped<IWeaponSyncClientService>(player, 
                service => service.OnWeaponSwitched(notification));

            Log($"武器切换已广播: {notification.CurrentWeaponSlot} (场景: {player.CurrentScenelData.SceneName})", ConsoleColor.Cyan);
        }

        /// <summary>
        /// 广播武器射击特效（只发送给同房间且同场景的玩家）
        /// </summary>
        private void BroadcastWeaponFireToRoom(PlayerInfo player, WeaponFireData fireData)
        {
            ServerContext.Broadcast.BroadcastToSceneTyped<IWeaponSyncClientService>(player, 
                service => service.OnWeaponFired(fireData));
        }

        /// <summary>
        /// 当玩家加入房间时，发送所有玩家的武器数据
        /// </summary>
        public void SendAllWeaponDataToPlayer(string clientId, string roomId)
        {
            try
            {
                var roomPlayers = ServerContext.Rooms.GetRoomPlayers(roomId);
                if (roomPlayers == null || roomPlayers.Length == 0)
                {
                    Log($"房间 {roomId} 没有其他玩家，跳过发送武器数据", ConsoleColor.Yellow);
                    return;
                }

                var allWeaponData = new AllPlayersWeaponData();

                foreach (var player in roomPlayers)
                {
                    if (player.WeaponData != null && player.WeaponData.GetEquippedCount() > 0)
                    {
                        allWeaponData.PlayersWeapons[player.SteamId] = player.WeaponData;
                    }
                }

                var clientContext = ServerContext.Server.GetClientContext(clientId);
                if (clientContext != null)
                {
                    clientContext.Call<IWeaponSyncClientService>()
                        .OnAllPlayersWeaponReceived(allWeaponData);
                }
            }
            catch (Exception ex)
            {
                Log($"SendAllWeaponDataToPlayer 失败: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// 批量广播武器射击特效（只发送给同房间且同场景的玩家）
        /// 🚀 性能优化：霰弹枪 8 发弹丸一次性广播
        /// </summary>
        private void BroadcastWeaponFireBatchToRoom(PlayerInfo player, WeaponFireBatchData batchData)
        {
            // 转换为 WeaponFireData 数组并逐个发送
            var fireDataArray = batchData.ToFireDataArray();
            
            foreach (var fireData in fireDataArray)
            {
                ServerContext.Broadcast.BroadcastToSceneTyped<IWeaponSyncClientService>(player, 
                    service => service.OnWeaponFired(fireData));
            }
        }

        private void Log(string message, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[WeaponSyncService] {message}");
            Console.ResetColor();
        }
    }
}

