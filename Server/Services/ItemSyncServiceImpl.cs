using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Managers;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;

namespace DuckyNet.Server.Services
{
    /// <summary>
    /// 物品同步服务实现
    /// 管理所有掉落物品，分配全局 DropId，广播给房间内玩家
    /// </summary>
    public class ItemSyncServiceImpl : IItemSyncService
    {
        private readonly RpcServer _server;
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;

        // DropId 生成器（原子递增）
        private uint _nextDropId = 1;
        private readonly object _dropIdLock = new object();

        // 物品映射：DropId -> (RoomId, ItemData)
        private readonly ConcurrentDictionary<uint, (string RoomId, ItemDropData ItemData)> _droppedItems 
            = new ConcurrentDictionary<uint, (string, ItemDropData)>();

        public ItemSyncServiceImpl(RpcServer server, PlayerManager playerManager, RoomManager roomManager)
        {
            _server = server;
            _playerManager = playerManager;
            _roomManager = roomManager;
        }

        /// <summary>
        /// 丢弃物品
        /// </summary>
        public async Task<uint> DropItemAsync(IClientContext client, ItemDropData dropData)
        {
            try
            {
                // 获取玩家信息
                var player = _playerManager.GetPlayer(client.ClientId);
                if (player == null)
                {
                    Console.WriteLine($"[ItemSyncService] 丢弃失败 - 未找到玩家: ClientId={client.ClientId}");
                    return 0;
                }

                // 获取玩家所在房间
                var room = _roomManager.GetPlayerRoom(player);
                if (room == null)
                {
                    // 不在房间中：允许丢弃，但不同步（仅本地可见）
                    Console.WriteLine($"[ItemSyncService] 玩家不在房间，物品仅本地可见 - Player={player.SteamName}");
                    return 0; // 返回 0 表示仅本地丢弃
                }

                // 分配全局 DropId
                uint dropId = AllocateDropId();
                dropData.DropId = dropId;
                dropData.DroppedByPlayerId = player.SteamId;

                // 记录物品
                _droppedItems[dropId] = (room.RoomId, dropData);

                Console.WriteLine($"[ItemSyncService] 物品丢弃 - DropId={dropId}, Item={dropData.ItemName}, " +
                                 $"Player={player.SteamName}, Room={room.RoomId}, IsDefault={dropData.IsDefaultItem}");

                // 广播到同场景的其他玩家（排除自己）
                BroadcastToSameScene(room.RoomId, player, client.ClientId, 
                    (targetContext) => targetContext.Call<IItemSyncClientService>().OnRemoteItemDropped(dropData));

                Console.WriteLine($"[ItemSyncService] 已广播到房间 {room.RoomId} 同场景的其他玩家");

                return await Task.FromResult(dropId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ItemSyncService] 丢弃异常: {ex.Message}\n{ex.StackTrace}");
                return 0;
            }
        }

        /// <summary>
        /// 拾取物品
        /// </summary>
        public async Task<bool> PickupItemAsync(IClientContext client, ItemPickupRequest request)
        {
            try
            {
                // 获取玩家信息
                var player = _playerManager.GetPlayer(client.ClientId);
                if (player == null)
                {
                    Console.WriteLine($"[ItemSyncService] 拾取失败 - 未找到玩家: ClientId={client.ClientId}");
                    return false;
                }

                // 获取玩家所在房间
                var room = _roomManager.GetPlayerRoom(player);
                if (room == null)
                {
                    Console.WriteLine($"[ItemSyncService] 拾取失败 - 玩家不在房间中: {player.SteamName}");
                    return false;
                }

                // 检查物品是否存在
                if (!_droppedItems.TryRemove(request.DropId, out var itemInfo))
                {
                    Console.WriteLine($"[ItemSyncService] 拾取失败 - 物品不存在: DropId={request.DropId}");
                    return false;
                }

                // 验证房间
                if (itemInfo.RoomId != room.RoomId)
                {
                    Console.WriteLine($"[ItemSyncService] 拾取失败 - 物品不在同一房间: DropId={request.DropId}, " +
                                     $"ItemRoom={itemInfo.RoomId}, PlayerRoom={room.RoomId}");
                    return false;
                }

                Console.WriteLine($"[ItemSyncService] 物品拾取 - DropId={request.DropId}, Item={itemInfo.ItemData.ItemName}, " +
                                 $"Player={player.SteamName}, Room={room.RoomId}");

                // 广播到同场景的其他玩家（排除自己）
                BroadcastToSameScene(room.RoomId, player, client.ClientId, 
                    (targetContext) => targetContext.Call<IItemSyncClientService>().OnRemoteItemPickedUp(request.DropId, player.SteamId));

                Console.WriteLine($"[ItemSyncService] 已广播拾取到房间 {room.RoomId} 同场景的其他玩家");

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ItemSyncService] 拾取异常: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 分配新的 DropId
        /// </summary>
        private uint AllocateDropId()
        {
            lock (_dropIdLock)
            {
                uint id = _nextDropId++;
                // 防止溢出（从 1 开始）
                if (_nextDropId == 0)
                {
                    _nextDropId = 1;
                }
                return id;
            }
        }

        /// <summary>
        /// 清理指定房间的所有物品（房间关闭时调用）
        /// </summary>
        public void ClearRoomItems(string roomId)
        {
            int removedCount = 0;
            foreach (var kvp in _droppedItems)
            {
                if (kvp.Value.RoomId == roomId)
                {
                    if (_droppedItems.TryRemove(kvp.Key, out _))
                    {
                        removedCount++;
                    }
                }
            }

            if (removedCount > 0)
            {
                Console.WriteLine($"[ItemSyncService] 已清理房间 {roomId} 的 {removedCount} 个掉落物品");
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStats()
        {
            return $"ItemSyncService Stats:\n" +
                   $"  Total Dropped Items: {_droppedItems.Count}\n" +
                   $"  Next DropId: {_nextDropId}";
        }

        /// <summary>
        /// 广播到同场景的玩家（排除发送者）
        /// </summary>
        private void BroadcastToSameScene(string roomId, PlayerInfo senderPlayer, string exceptClientId, Action<IClientContext> action)
        {
            try
            {
                // 获取房间内的所有玩家
                var roomPlayers = _roomManager.GetRoomPlayers(roomId);
                int notifiedCount = 0;

                // 遍历房间内的每个玩家
                foreach (var targetPlayer in roomPlayers)
                {
                    // 条件1: 跳过发送者自己
                    if (targetPlayer.SteamId == senderPlayer.SteamId)
                    {
                        continue;
                    }

                    // 条件2: 检查是否在同一个房间（冗余检查，但保证安全）
                    var targetRoom = _roomManager.GetPlayerRoom(targetPlayer);
                    if (targetRoom == null || targetRoom.RoomId != roomId)
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
                        Console.WriteLine($"[ItemSyncService] ⚠️ 目标玩家 {targetPlayer.SteamName} 无 ClientId");
                        continue;
                    }

                    var targetClientContext = _server.GetClientContext(targetClientId);
                    if (targetClientContext != null)
                    {
                        try
                        {
                            // 执行回调（发送消息）
                            action(targetClientContext);
                            notifiedCount++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ItemSyncService] ❌ 向 {targetPlayer.SteamName} 发送消息失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[ItemSyncService] ⚠️ 目标玩家 {targetPlayer.SteamName} 无上下文");
                    }
                }

                Console.WriteLine($"[ItemSyncService] 已通知 {notifiedCount} 个同场景玩家");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ItemSyncService] 广播到同场景异常: {ex.Message}");
            }
        }
    }
}

