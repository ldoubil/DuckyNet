using UnityEngine;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 单位（怪物/NPC）创建事件
    /// 当场景中生成新的 CharacterMainControl 时触发
    /// </summary>
    public class CharacterSpawnedEvent
    {
        /// <summary>
        /// 角色控制器
        /// </summary>
        public object CharacterMainControl { get; }
        
        /// <summary>
        /// 角色 GameObject（可能为 null）
        /// </summary>
        public GameObject? GameObject { get; }
        
        /// <summary>
        /// 自动生成的唯一 ID（用于网络同步）
        /// </summary>
        public int CharacterId { get; }

        public CharacterSpawnedEvent(object characterMainControl, GameObject? gameObject, int characterId)
        {
            CharacterMainControl = characterMainControl;
            GameObject = gameObject;
            CharacterId = characterId;
        }
    }

    /// <summary>
    /// 单位销毁事件
    /// 当 CharacterMainControl 被销毁时触发
    /// </summary>
    public class CharacterDestroyedEvent
    {
        /// <summary>
        /// 角色控制器
        /// </summary>
        public object CharacterMainControl { get; }
        
        /// <summary>
        /// 角色 GameObject（可能为 null）
        /// </summary>
        public GameObject? GameObject { get; }
        
        /// <summary>
        /// 角色 ID
        /// </summary>
        public int CharacterId { get; }

        public CharacterDestroyedEvent(object characterMainControl, GameObject? gameObject, int characterId)
        {
            CharacterMainControl = characterMainControl;
            GameObject = gameObject;
            CharacterId = characterId;
        }
    }

    /// <summary>
    /// 单位死亡事件
    /// 当 Health 组件触发死亡时触发（生命值为0）
    /// </summary>
    public class CharacterDeathEvent
    {
        /// <summary>
        /// Health 组件
        /// </summary>
        public object Health { get; }
        
        /// <summary>
        /// 伤害信息
        /// </summary>
        public object DamageInfo { get; }
        
        /// <summary>
        /// 角色控制器（如果可用）
        /// </summary>
        public object? CharacterMainControl { get; }
        
        /// <summary>
        /// 角色 GameObject
        /// </summary>
        public GameObject? GameObject { get; }
        
        /// <summary>
        /// 角色 ID（与创建/销毁事件相同的ID）
        /// </summary>
        public int CharacterId { get; }

        public CharacterDeathEvent(object health, object damageInfo, object? characterMainControl, GameObject? gameObject, int characterId)
        {
            Health = health;
            DamageInfo = damageInfo;
            CharacterMainControl = characterMainControl;
            GameObject = gameObject;
            CharacterId = characterId;
        }
    }
}

