using ItemStatsSystem;
using ItemStatsSystem.Items;
using WeaponSlotType = DuckyNet.Shared.Data.WeaponSlotType;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 武器槽位变化事件
    /// 当本地玩家的武器被装备或卸下时触发
    /// 
    /// 🎯 作用域：
    /// - 仅针对本地玩家（主角色）的武器变更
    /// - 不会触发其他玩家或NPC的武器事件
    /// 
    /// ⚠️ 重要提示：
    /// - 此事件持有对游戏对象的引用，订阅者应立即处理事件
    /// - Weapon 对象可能在事件处理后被销毁，不要在异步操作中使用
    /// - 订阅者应在不再需要时取消订阅，避免内存泄漏
    /// </summary>
    public class WeaponSlotChangedEvent
    {
        /// <summary>槽位对象</summary>
        public object? Slot { get; }

        /// <summary>武器物品（null表示卸下）</summary>
        public object? Weapon { get; }

        /// <summary>槽位类型枚举</summary>
        public WeaponSlotType SlotType { get; }

        /// <summary>槽位类型名称（中文）</summary>
        public string SlotTypeName { get; }

        /// <summary>是否是装备操作（false表示卸下）</summary>
        public bool IsEquipped { get; }

        public WeaponSlotChangedEvent(
            object? slot,
            object? weapon,
            WeaponSlotType slotType,
            string slotTypeName,
            bool isEquipped)
        {
            Slot = slot;
            Weapon = weapon;
            SlotType = slotType;
            SlotTypeName = slotTypeName;
            IsEquipped = isEquipped;
        }
    }
}

