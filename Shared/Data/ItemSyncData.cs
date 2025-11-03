using System;
using System.Collections.Generic;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// 物品丢弃数据
    /// </summary>
    [Serializable]
    public class ItemDropData
    {
        /// <summary>
        /// 服务器分配的全局唯一物品ID
        /// </summary>
        public uint DropId { get; set; }

        /// <summary>
        /// 物品类型ID
        /// </summary>
        public int ItemTypeId { get; set; }

        /// <summary>
        /// 物品显示名称
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// 丢弃位置
        /// </summary>
        public SerializableVector3 Position { get; set; }

        /// <summary>
        /// 丢弃方向
        /// </summary>
        public SerializableVector3 Direction { get; set; }

        /// <summary>
        /// 是否创建刚体
        /// </summary>
        public bool CreateRigidbody { get; set; }

        /// <summary>
        /// 随机角度
        /// </summary>
        public float RandomAngle { get; set; }

        /// <summary>
        /// 物品完整数据（Base64编码的压缩数据）
        /// 如果为空，表示使用默认物品（增量同步优化）
        /// </summary>
        public string ItemDataCompressed { get; set; } = string.Empty;

        /// <summary>
        /// 是否为默认物品（无自定义修改）
        /// </summary>
        public bool IsDefaultItem { get; set; }

        /// <summary>
        /// 丢弃者的 Steam ID
        /// </summary>
        public string DroppedByPlayerId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 物品拾取请求
    /// </summary>
    [Serializable]
    public class ItemPickupRequest
    {
        /// <summary>
        /// 要拾取的物品 DropId
        /// </summary>
        public uint DropId { get; set; }

        /// <summary>
        /// 拾取者的 Steam ID（由服务器验证）
        /// </summary>
        public string PickedByPlayerId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 可序列化的 Vector3（Unity 的 Vector3 不可直接序列化）
    /// </summary>
    [Serializable]
    public struct SerializableVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public SerializableVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// 简化的可序列化物品数据（用于网络传输）
    /// </summary>
    [Serializable]
    public class SerializableItemData
    {
        public int RootInstanceID { get; set; }
        public List<SerializableEntry> Entries { get; set; } = new List<SerializableEntry>();
    }

    [Serializable]
    public class SerializableEntry
    {
        public int InstanceID { get; set; }
        public int TypeID { get; set; }
        public List<SerializableVariable> Variables { get; set; } = new List<SerializableVariable>();
        public List<SerializableSlot> Slots { get; set; } = new List<SerializableSlot>();
        public List<SerializableInventoryItem> Inventory { get; set; } = new List<SerializableInventoryItem>();
        public List<int> InventorySortLocks { get; set; } = new List<int>();
    }

    [Serializable]
    public class SerializableVariable
    {
        public string Key { get; set; } = "";
        public int DataType { get; set; }
        public int IntValue { get; set; }
        public float FloatValue { get; set; }
        public string StringValue { get; set; } = "";
        public bool BoolValue { get; set; }
    }

    [Serializable]
    public class SerializableSlot
    {
        public string SlotName { get; set; } = "";
        public int ItemInstanceID { get; set; }
    }

    [Serializable]
    public class SerializableInventoryItem
    {
        public int Position { get; set; }
        public int ItemInstanceID { get; set; }
    }
}

