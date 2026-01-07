using DuckyNet.Shared.Events;
using EquipmentSlotType = DuckyNet.Shared.Data.EquipmentSlotType;

namespace DuckyNet.Client.Core.EventBus.Events
{
    // 使用 Shared 中定义的 EquipmentSlotType，避免重复定义

    /// <summary>
    /// 装备槽位变更事件基类
    /// 当本地玩家的装备被装备或卸下时触发
    /// 
    /// 🎯 作用域：
    /// - 仅针对本地玩家（主角色）的装备变更
    /// - 不会触发其他玩家或NPC的装备事件
    /// 
    /// ⚠️ 重要提示：
    /// - 此事件持有对游戏对象的引用，订阅者应立即处理事件，避免长期持有引用
    /// - Slot 和 EquippedItem 对象可能在事件处理后被销毁，不要在异步操作中使用
    /// - 订阅者应在不再需要时取消订阅，避免内存泄漏
    /// - 事件在游戏主线程触发，处理逻辑应避免阻塞操作
    /// 
    /// 📖 使用示例：
    /// <code>
    /// GameContext.Instance.EventBus.Subscribe&lt;ArmorSlotChangedEvent&gt;(evt =>
    /// {
    ///     if (evt.EquippedItem is UnityEngine.Object item)
    ///     {
    ///         Debug.Log($"本地玩家装备了: {item.name}");
    ///     }
    /// });
    /// </code>
    /// </summary>
    public class EquipmentSlotChangedEvent : EventBase
    {
        /// <summary>槽位类型</summary>
        public EquipmentSlotType SlotType { get; }

        /// <summary>槽位对象 (ItemStatsSystem.Items.Slot，可能为 null)</summary>
        public object? Slot { get; }

        /// <summary>装备的物品 (ItemStatsSystem.Items.Item，null表示卸下)</summary>
        public object? EquippedItem { get; }

        /// <summary>是否是装备操作（false表示卸下）</summary>
        public bool IsEquipped => EquippedItem != null;

        /// <summary>装备控制器实例 (CharacterEquipmentController)</summary>
        public object EquipmentController { get; }

        public EquipmentSlotChangedEvent(
            EquipmentSlotType slotType,
            object? slot,
            object? equippedItem,
            object equipmentController)
        {
            SlotType = slotType;
            Slot = slot;
            EquippedItem = equippedItem;
            EquipmentController = equipmentController;
        }
    }

    /// <summary>
    /// 护甲槽位变更事件
    /// </summary>
    public class ArmorSlotChangedEvent : EquipmentSlotChangedEvent
    {
        public ArmorSlotChangedEvent(object? slot, object? equippedItem, object equipmentController)
            : base(EquipmentSlotType.Armor, slot, equippedItem, equipmentController)
        {
        }
    }

    /// <summary>
    /// 头盔槽位变更事件
    /// </summary>
    public class HelmetSlotChangedEvent : EquipmentSlotChangedEvent
    {
        public HelmetSlotChangedEvent(object? slot, object? equippedItem, object equipmentController)
            : base(EquipmentSlotType.Helmet, slot, equippedItem, equipmentController)
        {
        }
    }

    /// <summary>
    /// 面罩槽位变更事件
    /// </summary>
    public class FaceMaskSlotChangedEvent : EquipmentSlotChangedEvent
    {
        public FaceMaskSlotChangedEvent(object? slot, object? equippedItem, object equipmentController)
            : base(EquipmentSlotType.FaceMask, slot, equippedItem, equipmentController)
        {
        }
    }

    /// <summary>
    /// 背包槽位变更事件
    /// </summary>
    public class BackpackSlotChangedEvent : EquipmentSlotChangedEvent
    {
        public BackpackSlotChangedEvent(object? slot, object? equippedItem, object equipmentController)
            : base(EquipmentSlotType.Backpack, slot, equippedItem, equipmentController)
        {
        }
    }

    /// <summary>
    /// 耳机槽位变更事件
    /// </summary>
    public class HeadsetSlotChangedEvent : EquipmentSlotChangedEvent
    {
        public HeadsetSlotChangedEvent(object? slot, object? equippedItem, object equipmentController)
            : base(EquipmentSlotType.Headset, slot, equippedItem, equipmentController)
        {
        }
    }
}
