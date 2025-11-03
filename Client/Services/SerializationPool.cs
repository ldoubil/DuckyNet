using System.Collections.Generic;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 序列化数据对象池
    /// 减少 GC 压力，复用序列化对象
    /// </summary>
    public static class SerializationPool
    {
        private static readonly Stack<SerializableItemData> _itemDataPool = new Stack<SerializableItemData>();
        private static readonly Stack<SerializableEntry> _entryPool = new Stack<SerializableEntry>();
        private static readonly Stack<SerializableVariable> _variablePool = new Stack<SerializableVariable>();
        private static readonly Stack<SerializableSlot> _slotPool = new Stack<SerializableSlot>();
        private static readonly Stack<SerializableInventoryItem> _inventoryItemPool = new Stack<SerializableInventoryItem>();

        private const int MAX_POOL_SIZE = 50;

        #region ItemData Pool

        public static SerializableItemData GetItemData()
        {
            lock (_itemDataPool)
            {
                if (_itemDataPool.Count > 0)
                {
                    var data = _itemDataPool.Pop();
                    data.Entries.Clear();
                    return data;
                }
            }
            return new SerializableItemData();
        }

        public static void ReleaseItemData(SerializableItemData data)
        {
            if (data == null) return;

            lock (_itemDataPool)
            {
                if (_itemDataPool.Count < MAX_POOL_SIZE)
                {
                    data.Entries.Clear();
                    _itemDataPool.Push(data);
                }
            }
        }

        #endregion

        #region Entry Pool

        public static SerializableEntry GetEntry()
        {
            lock (_entryPool)
            {
                if (_entryPool.Count > 0)
                {
                    var entry = _entryPool.Pop();
                    entry.Variables.Clear();
                    entry.Slots.Clear();
                    entry.Inventory.Clear();
                    entry.InventorySortLocks.Clear();
                    return entry;
                }
            }
            return new SerializableEntry();
        }

        public static void ReleaseEntry(SerializableEntry entry)
        {
            if (entry == null) return;

            // 释放嵌套对象
            foreach (var variable in entry.Variables)
            {
                ReleaseVariable(variable);
            }
            foreach (var slot in entry.Slots)
            {
                ReleaseSlot(slot);
            }
            foreach (var inv in entry.Inventory)
            {
                ReleaseInventoryItem(inv);
            }

            lock (_entryPool)
            {
                if (_entryPool.Count < MAX_POOL_SIZE)
                {
                    entry.Variables.Clear();
                    entry.Slots.Clear();
                    entry.Inventory.Clear();
                    entry.InventorySortLocks.Clear();
                    _entryPool.Push(entry);
                }
            }
        }

        #endregion

        #region Variable Pool

        public static SerializableVariable GetVariable()
        {
            lock (_variablePool)
            {
                if (_variablePool.Count > 0)
                {
                    return _variablePool.Pop();
                }
            }
            return new SerializableVariable();
        }

        public static void ReleaseVariable(SerializableVariable variable)
        {
            if (variable == null) return;

            lock (_variablePool)
            {
                if (_variablePool.Count < MAX_POOL_SIZE)
                {
                    variable.Key = "";
                    variable.StringValue = "";
                    _variablePool.Push(variable);
                }
            }
        }

        #endregion

        #region Slot Pool

        public static SerializableSlot GetSlot()
        {
            lock (_slotPool)
            {
                if (_slotPool.Count > 0)
                {
                    return _slotPool.Pop();
                }
            }
            return new SerializableSlot();
        }

        public static void ReleaseSlot(SerializableSlot slot)
        {
            if (slot == null) return;

            lock (_slotPool)
            {
                if (_slotPool.Count < MAX_POOL_SIZE)
                {
                    slot.SlotName = "";
                    _slotPool.Push(slot);
                }
            }
        }

        #endregion

        #region InventoryItem Pool

        public static SerializableInventoryItem GetInventoryItem()
        {
            lock (_inventoryItemPool)
            {
                if (_inventoryItemPool.Count > 0)
                {
                    return _inventoryItemPool.Pop();
                }
            }
            return new SerializableInventoryItem();
        }

        public static void ReleaseInventoryItem(SerializableInventoryItem item)
        {
            if (item == null) return;

            lock (_inventoryItemPool)
            {
                if (_inventoryItemPool.Count < MAX_POOL_SIZE)
                {
                    _inventoryItemPool.Push(item);
                }
            }
        }

        #endregion

        /// <summary>
        /// 清空所有对象池（用于测试或重置）
        /// </summary>
        public static void ClearAll()
        {
            lock (_itemDataPool) _itemDataPool.Clear();
            lock (_entryPool) _entryPool.Clear();
            lock (_variablePool) _variablePool.Clear();
            lock (_slotPool) _slotPool.Clear();
            lock (_inventoryItemPool) _inventoryItemPool.Clear();
        }

        /// <summary>
        /// 获取对象池统计信息
        /// </summary>
        public static string GetPoolStats()
        {
            return $"SerializationPool Stats:\n" +
                   $"  ItemData: {_itemDataPool.Count}\n" +
                   $"  Entry: {_entryPool.Count}\n" +
                   $"  Variable: {_variablePool.Count}\n" +
                   $"  Slot: {_slotPool.Count}\n" +
                   $"  InventoryItem: {_inventoryItemPool.Count}";
        }
    }
}

