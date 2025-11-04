using System;
using System.Collections.Generic;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// 装备槽位类型（与客户端保持一致）
    /// </summary>
    public enum EquipmentSlotType : byte
    {
        /// <summary>护甲</summary>
        Armor = 0,
        /// <summary>头盔</summary>
        Helmet = 1,
        /// <summary>面罩</summary>
        FaceMask = 2,
        /// <summary>背包</summary>
        Backpack = 3,
        /// <summary>耳机</summary>
        Headset = 4
    }

    /// <summary>
    /// 玩家装备数据
    /// Key: EquipmentSlotType, Value: ItemTypeID (null 或 0 表示该槽位为空)
    /// </summary>
    [Serializable]
    public class PlayerEquipmentData
    {
        /// <summary>
        /// 装备槽位数据
        /// </summary>
        public Dictionary<EquipmentSlotType, int> Equipment { get; set; } = new Dictionary<EquipmentSlotType, int>();

        /// <summary>
        /// 获取指定槽位的装备 TypeID
        /// </summary>
        public int? GetEquipment(EquipmentSlotType slotType)
        {
            if (Equipment.TryGetValue(slotType, out int typeId) && typeId > 0)
            {
                return typeId;
            }
            return null;
        }

        /// <summary>
        /// 设置指定槽位的装备
        /// </summary>
        public void SetEquipment(EquipmentSlotType slotType, int? itemTypeId)
        {
            if (itemTypeId.HasValue && itemTypeId.Value > 0)
            {
                Equipment[slotType] = itemTypeId.Value;
            }
            else
            {
                // 卸下装备 - 删除该键
                Equipment.Remove(slotType);
            }
        }

        /// <summary>
        /// 清空所有装备
        /// </summary>
        public void ClearAll()
        {
            Equipment.Clear();
        }

        /// <summary>
        /// 克隆装备数据
        /// </summary>
        public PlayerEquipmentData Clone()
        {
            var clone = new PlayerEquipmentData();
            foreach (var kvp in Equipment)
            {
                clone.Equipment[kvp.Key] = kvp.Value;
            }
            return clone;
        }

        /// <summary>
        /// 获取已装备的槽位数量
        /// </summary>
        public int GetEquippedCount()
        {
            return Equipment.Count;
        }
    }

    /// <summary>
    /// 装备槽位更新请求
    /// </summary>
    [Serializable]
    public class EquipmentSlotUpdateRequest
    {
        /// <summary>槽位类型</summary>
        public EquipmentSlotType SlotType { get; set; }

        /// <summary>物品 TypeID（null 或 0 表示卸下）</summary>
        public int? ItemTypeId { get; set; }
    }

    /// <summary>
    /// 装备槽位更新通知（服务器广播给其他玩家）
    /// </summary>
    [Serializable]
    public class EquipmentSlotUpdateNotification
    {
        /// <summary>玩家ID</summary>
        public string PlayerId { get; set; } = "";

        /// <summary>槽位类型</summary>
        public EquipmentSlotType SlotType { get; set; }

        /// <summary>物品 TypeID（null 或 0 表示卸下）</summary>
        public int? ItemTypeId { get; set; }
    }

    /// <summary>
    /// 批量装备数据（加入房间时发送）
    /// </summary>
    [Serializable]
    public class AllPlayersEquipmentData
    {
        /// <summary>
        /// 所有玩家的装备数据
        /// Key: PlayerId, Value: 该玩家的装备数据
        /// </summary>
        public Dictionary<string, PlayerEquipmentData> PlayersEquipment { get; set; } 
            = new Dictionary<string, PlayerEquipmentData>();
    }
}

