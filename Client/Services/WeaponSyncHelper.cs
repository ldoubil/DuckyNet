using System;
using System.IO;
using System.Collections.Generic;
using ItemStatsSystem;
using ItemStatsSystem.Data;
using ItemStatsSystem.Items;
using DuckyNet.Shared.Data;
using NetSerializer;
using UnityEngine;
using Duckov.Utilities;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 武器同步辅助类
    /// 负责武器物品的序列化和反序列化（参考 ItemNetworkCoordinator）
    /// </summary>
    public static class WeaponSyncHelper
    {
        // 复用序列化器，避免重复创建
        private static Serializer? _serializer;

        private static Serializer GetSerializer()
        {
            if (_serializer == null)
            {
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
            return _serializer;
        }

        /// <summary>
        /// 创建武器槽位更新请求
        /// </summary>
        public static WeaponSlotUpdateRequest CreateWeaponSlotUpdateRequest(WeaponSlotType slotType, Item weaponItem)
        {
            if (weaponItem == null)
            {
                throw new ArgumentNullException(nameof(weaponItem));
            }

            var request = new WeaponSlotUpdateRequest
            {
                SlotType = slotType,
                ItemTypeId = weaponItem.TypeID,
                ItemName = weaponItem.DisplayName
            };

            // 检查是否为默认物品（增量同步优化）
            if (IsDefaultItem(weaponItem))
            {
                request.IsDefaultItem = true;
                request.ItemDataCompressed = string.Empty;
                Debug.Log($"[WeaponSyncHelper] 增量同步 - 默认武器，不传输数据: {weaponItem.DisplayName}");
            }
            else
            {
                request.IsDefaultItem = false;
                request.ItemDataCompressed = SerializeAndCompressItem(weaponItem);
                Debug.Log($"[WeaponSyncHelper] 完整同步 - 自定义武器，数据长度={request.ItemDataCompressed.Length}");
            }

            return request;
        }

        /// <summary>
        /// 检查是否为默认物品（没有配件、弹药等修改）
        /// </summary>
        private static bool IsDefaultItem(Item item)
        {
            // 检查是否有插槽内容（如配件）
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

            // 检查是否有库存物品（如弹药）
            if (item.Inventory != null && item.Inventory.GetItemCount() > 0)
            {
                return false;
            }

            // 检查是否有自定义变量
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
        /// 序列化物品数据
        /// </summary>
        private static string SerializeAndCompressItem(Item item)
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
                    GetSerializer().Serialize(ms, simpleData);
                    rawBytes = ms.ToArray();
                }

                Debug.Log($"[WeaponSyncHelper] 序列化完成: {rawBytes.Length} bytes");

                // Base64 编码
                return Convert.ToBase64String(rawBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponSyncHelper] 序列化失败: {ex.Message}\n{ex.StackTrace}");
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
        /// 反序列化物品数据
        /// </summary>
        public static Item? DeserializeItem(string base64Data, int itemTypeId)
        {
            try
            {
                if (string.IsNullOrEmpty(base64Data))
                {
                    // 默认物品 - 直接创建
                    Debug.Log($"[WeaponSyncHelper] 创建默认武器 - TypeId={itemTypeId}");
                    return ItemAssetsCollection.InstantiateSync(itemTypeId);
                }

                // Base64 解码
                byte[] rawBytes = Convert.FromBase64String(base64Data);
                Debug.Log($"[WeaponSyncHelper] 反序列化: {rawBytes.Length} bytes");

                // 反序列化
                SerializableItemData simpleData;
                using (var ms = new MemoryStream(rawBytes))
                {
                    simpleData = (SerializableItemData)GetSerializer().Deserialize(ms);
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

                Debug.Log($"[WeaponSyncHelper] 反序列化成功 - Entries={treeData.entries.Count}");

                // 实例化物品树
                var item = InstantiateItemTreeSync(treeData);

                if (item == null)
                {
                    Debug.LogError($"[WeaponSyncHelper] 无法创建武器 - RootTypeId={treeData.RootTypeID}");
                    return null;
                }

                Debug.Log($"[WeaponSyncHelper] 创建武器成功 - TypeId={item.TypeID}, Name={item.DisplayName}");
                return item;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponSyncHelper] 反序列化失败: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 同步实例化物品树
        /// </summary>
        private static Item? InstantiateItemTreeSync(ItemTreeData treeData)
        {
            var instanceMap = new Dictionary<int, Item>();

            // 第一步：创建所有物品实例
            foreach (var entry in treeData.entries)
            {
                var item = ItemAssetsCollection.InstantiateSync(entry.typeID);
                if (item == null)
                {
                    Debug.LogError($"[WeaponSyncHelper] 无法创建物品 TypeID={entry.typeID}");
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
    }
}

