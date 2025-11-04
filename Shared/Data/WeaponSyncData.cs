using System;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// 武器槽位类型
    /// </summary>
    public enum WeaponSlotType : byte
    {
        /// <summary>主武器</summary>
        PrimaryWeapon = 0,
        /// <summary>副武器</summary>
        SecondaryWeapon = 1,
        /// <summary>近战武器</summary>
        MeleeWeapon = 2
    }

    /// <summary>
    /// 武器槽位更新请求（客户端→服务器）
    /// </summary>
    [Serializable]
    public class WeaponSlotUpdateRequest
    {
        /// <summary>槽位类型</summary>
        public WeaponSlotType SlotType { get; set; }

        /// <summary>物品 TypeID</summary>
        public int ItemTypeId { get; set; }

        /// <summary>物品名称（用于日志）</summary>
        public string ItemName { get; set; } = "";

        /// <summary>
        /// 是否为默认物品（增量同步优化）
        /// 如果为 true，ItemDataCompressed 为空，接收端自动创建默认物品
        /// </summary>
        public bool IsDefaultItem { get; set; }

        /// <summary>
        /// 压缩的物品数据（Base64编码）
        /// 包含完整的物品树（槽位、背包、变量等）
        /// </summary>
        public string ItemDataCompressed { get; set; } = "";
    }

    /// <summary>
    /// 武器槽位卸下请求（客户端→服务器）
    /// </summary>
    [Serializable]
    public class WeaponSlotUnequipRequest
    {
        /// <summary>槽位类型</summary>
        public WeaponSlotType SlotType { get; set; }
    }

    /// <summary>
    /// 武器切换请求（客户端→服务器）
    /// 当玩家按1/2/3键切换武器时发送
    /// </summary>
    [Serializable]
    public class WeaponSwitchRequest
    {
        /// <summary>当前切换到的武器槽位</summary>
        public WeaponSlotType CurrentWeaponSlot { get; set; }
    }

    /// <summary>
    /// 武器切换通知（服务器→客户端）
    /// </summary>
    [Serializable]
    public class WeaponSwitchNotification
    {
        /// <summary>玩家ID</summary>
        public string PlayerId { get; set; } = "";

        /// <summary>当前武器槽位</summary>
        public WeaponSlotType CurrentWeaponSlot { get; set; }
    }

    /// <summary>
    /// 武器槽位更新通知（服务器→客户端）
    /// </summary>
    [Serializable]
    public class WeaponSlotUpdateNotification
    {
        /// <summary>玩家ID</summary>
        public string PlayerId { get; set; } = "";

        /// <summary>槽位类型</summary>
        public WeaponSlotType SlotType { get; set; }

        /// <summary>物品 TypeID（0表示卸下）</summary>
        public int ItemTypeId { get; set; }

        /// <summary>物品名称</summary>
        public string ItemName { get; set; } = "";

        /// <summary>是否为默认物品</summary>
        public bool IsDefaultItem { get; set; }

        /// <summary>压缩的物品数据</summary>
        public string ItemDataCompressed { get; set; } = "";
    }

    /// <summary>
    /// 玩家武器数据（存储在 PlayerInfo 中）
    /// </summary>
    [Serializable]
    public class PlayerWeaponData
    {
        /// <summary>主武器</summary>
        public WeaponItemData? PrimaryWeapon { get; set; }

        /// <summary>副武器</summary>
        public WeaponItemData? SecondaryWeapon { get; set; }

        /// <summary>近战武器</summary>
        public WeaponItemData? MeleeWeapon { get; set; }

        /// <summary>当前手持的武器槽位（用于显示）</summary>
        public WeaponSlotType? CurrentWeaponSlot { get; set; }

        /// <summary>
        /// 获取指定槽位的武器数据
        /// </summary>
        public WeaponItemData? GetWeapon(WeaponSlotType slotType)
        {
            return slotType switch
            {
                WeaponSlotType.PrimaryWeapon => PrimaryWeapon,
                WeaponSlotType.SecondaryWeapon => SecondaryWeapon,
                WeaponSlotType.MeleeWeapon => MeleeWeapon,
                _ => null
            };
        }

        /// <summary>
        /// 设置指定槽位的武器数据
        /// </summary>
        public void SetWeapon(WeaponSlotType slotType, WeaponItemData? weaponData)
        {
            switch (slotType)
            {
                case WeaponSlotType.PrimaryWeapon:
                    PrimaryWeapon = weaponData;
                    break;
                case WeaponSlotType.SecondaryWeapon:
                    SecondaryWeapon = weaponData;
                    break;
                case WeaponSlotType.MeleeWeapon:
                    MeleeWeapon = weaponData;
                    break;
            }
        }

        /// <summary>
        /// 清空所有武器
        /// </summary>
        public void ClearAll()
        {
            PrimaryWeapon = null;
            SecondaryWeapon = null;
            MeleeWeapon = null;
        }

        /// <summary>
        /// 获取已装备的武器数量
        /// </summary>
        public int GetEquippedCount()
        {
            int count = 0;
            if (PrimaryWeapon != null) count++;
            if (SecondaryWeapon != null) count++;
            if (MeleeWeapon != null) count++;
            return count;
        }
    }

    /// <summary>
    /// 单个武器的数据
    /// </summary>
    [Serializable]
    public class WeaponItemData
    {
        /// <summary>物品 TypeID</summary>
        public int ItemTypeId { get; set; }

        /// <summary>物品名称</summary>
        public string ItemName { get; set; } = "";

        /// <summary>是否为默认物品</summary>
        public bool IsDefaultItem { get; set; }

        /// <summary>压缩的物品数据（Base64编码的序列化数据）</summary>
        public string ItemDataCompressed { get; set; } = "";
    }

    /// <summary>
    /// 批量武器数据（加入房间时发送）
    /// </summary>
    [Serializable]
    public class AllPlayersWeaponData
    {
        /// <summary>
        /// 所有玩家的武器数据
        /// Key: PlayerId, Value: 该玩家的武器数据
        /// </summary>
        public System.Collections.Generic.Dictionary<string, PlayerWeaponData> PlayersWeapons { get; set; }
            = new System.Collections.Generic.Dictionary<string, PlayerWeaponData>();
    }
}

