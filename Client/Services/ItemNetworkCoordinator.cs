using UnityEngine;
using ItemStatsSystem;
using ItemStatsSystem.Data;
using Duckov.Utilities;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Services.Generated;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using NetSerializer;
using K4os.Compression.LZ4;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 物品网络协调器 - 协调本地与远程玩家的物品掉落和拾取
    /// 包含优化：对象池、LZ4压缩、增量同步
    /// </summary>
    public class ItemNetworkCoordinator
    {
        private readonly ItemSyncServiceClientProxy _itemSyncService;

        // 核心映射：DropId <-> Agent 双向映射
        private readonly Dictionary<uint, DuckovItemAgent> _dropIdToAgent = new Dictionary<uint, DuckovItemAgent>();
        private readonly Dictionary<DuckovItemAgent, uint> _agentToDropId = new Dictionary<DuckovItemAgent, uint>();

        // 状态追踪
        private readonly HashSet<Item> _pendingDrops = new HashSet<Item>(); // 防止重复丢弃
        private readonly HashSet<Item> _remoteCreating = new HashSet<Item>(); // 防止远程物品触发本地Drop patch

        // 序列化器（复用，避免重复创建）
        private readonly Serializer _serializer;

        public ItemNetworkCoordinator(ItemSyncServiceClientProxy itemSyncService)
        {
            _itemSyncService = itemSyncService ?? throw new ArgumentNullException(nameof(itemSyncService));

            // 初始化 NetSerializer（只创建一次）
            _serializer = new Serializer(new[]
            {
                typeof(SerializableItemData),
                typeof(SerializableEntry),
                typeof(SerializableVariable),
                typeof(SerializableSlot),
                typeof(SerializableInventoryItem),
                typeof(List<SerializableEntry>),
                typeof(List<SerializableVariable>),
                typeof(List<SerializableSlot>),
                typeof(List<SerializableInventoryItem>),
                typeof(List<int>)
            });
        }

        #region 本地操作

        /// <summary>
        /// 丢弃物品到场景
        /// </summary>
        public async Task<uint?> DropItemAsync(Item item, Vector3 position, bool createRigidbody, Vector3 dropDirection, float randomAngle)
        {
            if (item == null) return null;
            if (_pendingDrops.Contains(item))
            {
                Debug.LogWarning($"[ItemNetworkCoordinator] 物品正在丢弃中，忽略重复请求: {item.DisplayName}");
                return null;
            }

            _pendingDrops.Add(item);
            try
            {
                var dropData = CreateDropData(item, position, dropDirection, createRigidbody, randomAngle);

                // 异步调用服务器获取DropId
                uint dropId = await _itemSyncService.DropItemAsync(dropData);

                if (dropId == 0)
                {
                    // 不在房间中，物品仅本地可见（这是正常情况）
                    Debug.Log($"[ItemNetworkCoordinator] 物品仅本地可见（不在房间中） - Item={item.DisplayName}");
                    return null; // 返回 null 表示不注册网络映射
                }

                Debug.Log($"[ItemNetworkCoordinator] 发送丢弃成功 - DropId={dropId}, Item={item.DisplayName}");
                return dropId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemNetworkCoordinator] 发送丢弃失败: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
            finally
            {
                _pendingDrops.Remove(item);
            }
        }

        /// <summary>
        /// 注册本地掉落的物品Agent
        /// </summary>
        public void RegisterLocalDrop(uint dropId, DuckovItemAgent agent)
        {
            if (agent == null || dropId == 0)
            {
                Debug.LogWarning($"[ItemNetworkCoordinator] 注册失败 - 无效参数: DropId={dropId}, Agent={agent}");
                return;
            }

            // 添加网络标记
            var tag = agent.gameObject.GetOrAddComponent<NetworkDropTag>();
            tag.DropId = dropId;
            tag.IsLocalDrop = true;

            // 建立双向映射
            AddToMapping(dropId, agent);

            Debug.Log($"[ItemNetworkCoordinator] 注册本地物品 - DropId={dropId}");
        }

        /// <summary>
        /// 拾取物品（通知服务器）
        /// </summary>
        public async Task<bool> PickupItemAsync(uint dropId)
        {
            try
            {
                var request = new ItemPickupRequest { DropId = dropId };

                // 异步通知服务器
                bool success = await _itemSyncService.PickupItemAsync(request);

                Debug.Log($"[ItemNetworkCoordinator] 拾取请求已发送 - DropId={dropId}, Success={success}");
                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemNetworkCoordinator] 拾取失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 远程事件处理

        /// <summary>
        /// 处理远程玩家丢弃物品事件
        /// </summary>
        public void OnRemoteItemDropped(ItemDropData dropData)
        {
            try
            {
                // 过滤自己的物品（本地已创建）
                if (IsLocalPlayer(dropData.DroppedByPlayerId))
                {
                    Debug.Log($"[ItemNetworkCoordinator] 跳过本地物品 - DropId={dropData.DropId}");
                    return;
                }

                Debug.Log($"[ItemNetworkCoordinator] 收到远程掉落 - DropId={dropData.DropId}, Item={dropData.ItemName}, Player={dropData.DroppedByPlayerId}");

                // 创建远程物品
                CreateRemoteItem(dropData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemNetworkCoordinator] 处理远程掉落失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 处理远程玩家拾取物品事件
        /// </summary>
        public void OnRemoteItemPickedUp(uint dropId, string playerId)
        {
            try
            {
                // 过滤自己的操作（本地已处理）
                if (IsLocalPlayer(playerId))
                {
                    Debug.Log($"[ItemNetworkCoordinator] 跳过本地拾取 - DropId={dropId}");
                    return;
                }

                Debug.Log($"[ItemNetworkCoordinator] 收到远程拾取 - DropId={dropId}, Player={playerId}");

                // 销毁本地的物品Agent
                DestroyDroppedItem(dropId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemNetworkCoordinator] 处理远程拾取失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion

        #region 查询接口

        /// <summary>
        /// 根据Agent查找DropId
        /// </summary>
        public uint? GetDropIdByAgent(DuckovItemAgent agent)
        {
            if (agent != null && _agentToDropId.TryGetValue(agent, out var dropId))
            {
                return dropId;
            }
            return null;
        }

        /// <summary>
        /// 检查Item是否正在被远程创建
        /// </summary>
        public bool IsRemoteCreating(Item item)
        {
            return _remoteCreating.Contains(item);
        }

        /// <summary>
        /// 获取当前网络物品数量
        /// </summary>
        public int LocalDropCount => _agentToDropId.Count(kv => kv.Value > 0);

        public int RemoteDropCount => _dropIdToAgent.Count;

        public int PendingDropCount => _pendingDrops.Count;

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 创建丢弃数据（含增量同步优化）
        /// </summary>
        private ItemDropData CreateDropData(Item item, Vector3 position, Vector3 direction, bool createRigidbody, float randomAngle)
        {
            var dropData = new ItemDropData
            {
                ItemTypeId = item.TypeID,
                ItemName = item.DisplayName,
                Position = ToSerializableVector3(position),
                Direction = ToSerializableVector3(direction),
                CreateRigidbody = createRigidbody,
                RandomAngle = randomAngle
            };

            // 增量同步优化：检查是否为默认物品
            if (IsDefaultItem(item))
            {
                dropData.IsDefaultItem = true;
                dropData.ItemDataCompressed = string.Empty;
                Debug.Log($"[ItemNetworkCoordinator] 增量同步 - 默认物品，不传输数据: {item.DisplayName}");
            }
            else
            {
                dropData.IsDefaultItem = false;
                dropData.ItemDataCompressed = SerializeAndCompressItem(item);
                Debug.Log($"[ItemNetworkCoordinator] 完整同步 - 自定义物品，数据长度={dropData.ItemDataCompressed.Length}");
            }

            return dropData;
        }

        /// <summary>
        /// 检查是否为默认物品（增量同步优化）
        /// </summary>
        private bool IsDefaultItem(Item item)
        {
            // 检查是否有插槽内容
            if (item.Slots != null)
            {
                foreach (var slot in item.Slots)
                {
                    if (slot.Content != null)
                    {
                        return false;
                    }
                }
            }

            // 检查是否有库存物品
            if (item.Inventory != null && item.Inventory.GetItemCount() > 0)
            {
                return false;
            }

            // 检查是否有自定义变量（排除默认的 Count=1）
            if (item.Variables != null && item.Variables.Count > 0)
            {
                foreach (var variable in item.Variables)
                {
                    // 跳过默认的 Count=1
                    if (variable.Key == "Count" && variable.DataType == CustomDataType.Int && variable.GetInt() == 1)
                    {
                        continue;
                    }
                    // 发现非默认变量
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 序列化并压缩物品（LZ4 压缩）
        /// </summary>
        private string SerializeAndCompressItem(Item item)
        {
            SerializableItemData? simpleData = null;
            try
            {
                // 从对象池获取
                simpleData = SerializationPool.GetItemData();

                // 使用 ItemTreeData 获取完整物品树
                var treeData = ItemTreeData.FromItem(item);
                simpleData.RootInstanceID = treeData.rootInstanceID;

                // 转换为可序列化结构
                foreach (var entry in treeData.entries)
                {
                    var simpleEntry = SerializationPool.GetEntry();
                    simpleEntry.InstanceID = entry.instanceID;
                    simpleEntry.TypeID = entry.typeID;

                    // 转换 variables
                    foreach (var variable in entry.variables)
                    {
                        var simpleVar = SerializationPool.GetVariable();
                        simpleVar.Key = variable.Key ?? "";
                        simpleVar.DataType = (int)variable.DataType;

                        switch (variable.DataType)
                        {
                            case CustomDataType.Int:
                                simpleVar.IntValue = variable.GetInt();
                                break;
                            case CustomDataType.Float:
                                simpleVar.FloatValue = variable.GetFloat();
                                break;
                            case CustomDataType.String:
                                simpleVar.StringValue = variable.GetString() ?? "";
                                break;
                            case CustomDataType.Bool:
                                simpleVar.BoolValue = variable.GetBool();
                                break;
                        }

                        simpleEntry.Variables.Add(simpleVar);
                    }

                    // 转换 slots
                    foreach (var slot in entry.slotContents)
                    {
                        var simpleSlot = SerializationPool.GetSlot();
                        simpleSlot.SlotName = slot.slot ?? "";
                        simpleSlot.ItemInstanceID = slot.instanceID;
                        simpleEntry.Slots.Add(simpleSlot);
                    }

                    // 转换 inventory
                    foreach (var inv in entry.inventory)
                    {
                        var simpleInv = SerializationPool.GetInventoryItem();
                        simpleInv.Position = inv.position;
                        simpleInv.ItemInstanceID = inv.instanceID;
                        simpleEntry.Inventory.Add(simpleInv);
                    }

                    // 转换 inventorySortLocks
                    simpleEntry.InventorySortLocks.AddRange(entry.inventorySortLocks);

                    simpleData.Entries.Add(simpleEntry);
                }

                // 序列化为字节数组
                byte[] rawBytes;
                using (var ms = new MemoryStream())
                {
                    _serializer.Serialize(ms, simpleData);
                    rawBytes = ms.ToArray();
                }

                // LZ4 压缩
                byte[] compressedBytes = K4os.Compression.LZ4.LZ4Pickler.Pickle(rawBytes, K4os.Compression.LZ4.LZ4Level.L00_FAST);

                // 计算压缩率
                float compressionRatio = (1 - (float)compressedBytes.Length / rawBytes.Length) * 100;

                Debug.Log($"[ItemNetworkCoordinator] 序列化+压缩: {rawBytes.Length} → {compressedBytes.Length} bytes (压缩率: {compressionRatio:F1}%)");

                // Base64 编码
                return Convert.ToBase64String(compressedBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemNetworkCoordinator] 序列化失败: {ex.Message}\n{ex.StackTrace}");
                return "";
            }
            finally
            {
                // 释放到对象池
                if (simpleData != null)
                {
                    SerializationPool.ReleaseItemData(simpleData);
                }
            }
        }

        /// <summary>
        /// 解压缩并反序列化物品
        /// </summary>
        private Item? DecompressAndDeserializeItem(string base64Data, int itemTypeId)
        {
            try
            {
                if (string.IsNullOrEmpty(base64Data))
                {
                    Debug.LogError($"[ItemNetworkCoordinator] 反序列化失败 - Base64数据为空");
                    return null;
                }

                // Base64 解码
                byte[] compressedBytes = Convert.FromBase64String(base64Data);

                // LZ4 解压缩
                byte[] rawBytes = K4os.Compression.LZ4.LZ4Pickler.Unpickle(compressedBytes);

                Debug.Log($"[ItemNetworkCoordinator] 解压缩: {compressedBytes.Length} → {rawBytes.Length} bytes");

                // 反序列化
                SerializableItemData simpleData;
                using (var ms = new MemoryStream(rawBytes))
                {
                    simpleData = (SerializableItemData)_serializer.Deserialize(ms);
                }

                // 转换为 ItemTreeData
                var treeData = new ItemTreeData
                {
                    rootInstanceID = simpleData.RootInstanceID,
                    entries = new List<ItemTreeData.DataEntry>()
                };

                foreach (var simpleEntry in simpleData.Entries)
                {
                    var entry = new ItemTreeData.DataEntry
                    {
                        instanceID = simpleEntry.InstanceID,
                        typeID = simpleEntry.TypeID,
                        variables = new List<CustomData>(),
                        slotContents = new List<ItemTreeData.SlotInstanceIDPair>(),
                        inventory = new List<ItemTreeData.InventoryDataEntry>(),
                        inventorySortLocks = new List<int>()
                    };

                    // 恢复 variables
                    foreach (var simpleVar in simpleEntry.Variables)
                    {
                        CustomData? customData = null;
                        switch ((CustomDataType)simpleVar.DataType)
                        {
                            case CustomDataType.Int:
                                customData = new CustomData(simpleVar.Key, simpleVar.IntValue);
                                break;
                            case CustomDataType.Float:
                                customData = new CustomData(simpleVar.Key, simpleVar.FloatValue);
                                break;
                            case CustomDataType.String:
                                customData = new CustomData(simpleVar.Key, simpleVar.StringValue);
                                break;
                            case CustomDataType.Bool:
                                customData = new CustomData(simpleVar.Key, simpleVar.BoolValue);
                                break;
                        }
                        if (customData != null)
                        {
                            entry.variables.Add(customData);
                        }
                    }

                    // 恢复 slots
                    foreach (var simpleSlot in simpleEntry.Slots)
                    {
                        entry.slotContents.Add(new ItemTreeData.SlotInstanceIDPair(
                            simpleSlot.SlotName,
                            simpleSlot.ItemInstanceID));
                    }

                    // 恢复 inventory
                    foreach (var simpleInv in simpleEntry.Inventory)
                    {
                        entry.inventory.Add(new ItemTreeData.InventoryDataEntry(
                            simpleInv.Position,
                            simpleInv.ItemInstanceID));
                    }

                    // 恢复 inventorySortLocks
                    entry.inventorySortLocks.AddRange(simpleEntry.InventorySortLocks);

                    treeData.entries.Add(entry);
                }

                Debug.Log($"[ItemNetworkCoordinator] 反序列化成功 - Entries={treeData.entries.Count}");

                // 实例化物品树
                var item = InstantiateItemTreeSync(treeData);

                if (item == null)
                {
                    Debug.LogError($"[ItemNetworkCoordinator] 无法创建物品 - RootTypeId={treeData.RootTypeID}");
                    return null;
                }

                Debug.Log($"[ItemNetworkCoordinator] 创建物品成功 - TypeId={item.TypeID}, Name={item.DisplayName}");
                return item;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemNetworkCoordinator] 反序列化失败: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 创建远程物品
        /// </summary>
        private void CreateRemoteItem(ItemDropData dropData)
        {
            Item? item = null;

            // 增量同步：如果是默认物品，直接创建
            if (dropData.IsDefaultItem)
            {
                Debug.Log($"[ItemNetworkCoordinator] 创建默认物品 - TypeId={dropData.ItemTypeId}");
                item = ItemAssetsCollection.InstantiateSync(dropData.ItemTypeId);
            }
            else
            {
                // 完整同步：反序列化完整物品数据
                item = DecompressAndDeserializeItem(dropData.ItemDataCompressed, dropData.ItemTypeId);
            }

            if (item == null)
            {
                Debug.LogError($"[ItemNetworkCoordinator] 创建物品失败 - DropId={dropData.DropId}");
                return;
            }

            // 标记为远程创建，防止触发Patch
            _remoteCreating.Add(item);
            try
            {
                var position = ToVector3(dropData.Position);
                var direction = ToVector3(dropData.Direction);

                var agent = ItemExtensions.Drop(item, position, dropData.CreateRigidbody, direction, dropData.RandomAngle);

                if (agent != null)
                {
                    // 添加网络标记
                    var tag = agent.gameObject.GetOrAddComponent<NetworkDropTag>();
                    tag.DropId = dropData.DropId;
                    tag.IsLocalDrop = false;

                    // 建立映射
                    AddToMapping(dropData.DropId, agent);

                    Debug.Log($"[ItemNetworkCoordinator] 远程物品创建成功 - DropId={dropData.DropId}");
                }
            }
            finally
            {
                _remoteCreating.Remove(item);
            }
        }

        /// <summary>
        /// 销毁掉落的物品
        /// </summary>
        private void DestroyDroppedItem(uint dropId)
        {
            if (_dropIdToAgent.TryGetValue(dropId, out var agent) && agent != null)
            {
                RemoveFromMapping(dropId, agent);
                UnityEngine.Object.Destroy(agent.gameObject);
                Debug.Log($"[ItemNetworkCoordinator] 物品已销毁 - DropId={dropId}");
            }
            else
            {
                Debug.LogWarning($"[ItemNetworkCoordinator] 未找到物品 - DropId={dropId}");
            }
        }

        private void AddToMapping(uint dropId, DuckovItemAgent agent)
        {
            _dropIdToAgent[dropId] = agent;
            _agentToDropId[agent] = dropId;
        }

        private void RemoveFromMapping(uint dropId, DuckovItemAgent agent)
        {
            _dropIdToAgent.Remove(dropId);
            _agentToDropId.Remove(agent);
        }

        private bool IsLocalPlayer(string playerId)
        {
            var localPlayerId = Core.GameContext.Instance?.PlayerManager?.LocalPlayer?.Info.SteamId;
            return !string.IsNullOrEmpty(localPlayerId) && localPlayerId == playerId;
        }

        private SerializableVector3 ToSerializableVector3(Vector3 vector) =>
            new SerializableVector3(vector.x, vector.y, vector.z);

        private Vector3 ToVector3(SerializableVector3 position) =>
            new Vector3(position.X, position.Y, position.Z);

        #endregion

        #region ItemTreeData 实例化

        /// <summary>
        /// 同步实例化物品树
        /// </summary>
        private Item? InstantiateItemTreeSync(ItemTreeData treeData)
        {
            var instanceMap = new Dictionary<int, Item>();

            // 第一步：创建所有物品实例
            foreach (var entry in treeData.entries)
            {
                var item = ItemAssetsCollection.InstantiateSync(entry.typeID);
                if (item == null)
                {
                    Debug.LogError($"[ItemNetworkCoordinator] 无法创建物品 TypeID={entry.typeID}");
                    return null;
                }

                // 恢复变量
                foreach (var variable in entry.variables)
                {
                    item.Variables.Add(new CustomData(variable));
                }

                instanceMap[entry.instanceID] = item;
            }

            // 第二步：建立物品间的关系
            foreach (var entry in treeData.entries)
            {
                var item = instanceMap[entry.instanceID];

                // 恢复插槽内容
                if (item.Slots != null)
                {
                    foreach (var slotPair in entry.slotContents)
                    {
                        if (instanceMap.TryGetValue(slotPair.instanceID, out var contentItem))
                        {
                            var slot = item.Slots.GetSlot(slotPair.slot);
                            if (slot != null)
                            {
                                slot.Plug(contentItem, out _);
                            }
                        }
                    }
                }

                // 恢复背包内容
                if (item.Inventory != null)
                {
                    foreach (var invEntry in entry.inventory)
                    {
                        if (instanceMap.TryGetValue(invEntry.instanceID, out var invItem))
                        {
                            item.Inventory.AddAt(invItem, invEntry.position);
                        }
                    }

                    // 恢复排序锁定
                    if (entry.inventorySortLocks != null)
                    {
                        foreach (var lockIndex in entry.inventorySortLocks)
                        {
                            item.Inventory.lockedIndexes.Add(lockIndex);
                        }
                    }
                }
            }

            // 返回根物品
            return instanceMap[treeData.rootInstanceID];
        }

        #endregion
    }

    /// <summary>
    /// 网络掉落物品标记组件
    /// </summary>
    public class NetworkDropTag : MonoBehaviour
    {
        public uint DropId { get; set; }
        public bool IsLocalDrop { get; set; }
    }

    /// <summary>
    /// Unity GameObject 扩展方法
    /// </summary>
    public static class GameObjectExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}

