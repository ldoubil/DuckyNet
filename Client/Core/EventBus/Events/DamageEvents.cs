using UnityEngine;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 伤害应用前事件
    /// 允许在伤害计算前修改伤害值、暴击率、护甲穿透等参数
    /// </summary>
    public class BeforeDamageAppliedEvent
    {
        /// <summary>
        /// Health 组件实例
        /// </summary>
        public object Health { get; }

        /// <summary>
        /// 原始 DamageInfo 对象（只读参考）
        /// </summary>
        public object OriginalDamageInfo { get; }

        /// <summary>
        /// 受伤角色的 GameObject（可能为 null）
        /// </summary>
        public GameObject? TargetGameObject { get; }

        /// <summary>
        /// 受伤角色的 CharacterMainControl（可能为 null）
        /// </summary>
        public object? TargetCharacter { get; }

        /// <summary>
        /// 角色 ID（如果已注册）
        /// </summary>
        public int? CharacterId { get; }



        // ===== 可修改的伤害参数 =====

        /// <summary>
        /// 基础伤害值（可修改）
        /// </summary>
        public float DamageValue { get; set; }

        /// <summary>
        /// 是否忽略护甲（可修改）
        /// </summary>
        public bool IgnoreArmor { get; set; }

        /// <summary>
        /// 是否忽略难度系数（可修改）
        /// </summary>
        public bool IgnoreDifficulty { get; set; }

        /// <summary>
        /// 暴击率（可修改，范围 0-1）
        /// </summary>
        public float CritRate { get; set; }

        /// <summary>
        /// 暴击伤害倍率（可修改）
        /// </summary>
        public float CritDamageFactor { get; set; }

        /// <summary>
        /// 护甲穿透（可修改）
        /// </summary>
        public float ArmorPiercing { get; set; }

        /// <summary>
        /// 是否取消伤害（设置为 true 将完全阻止伤害）
        /// </summary>
        public bool CancelDamage { get; set; }

        public BeforeDamageAppliedEvent(
            object health,
            object originalDamageInfo,
            GameObject? targetGameObject,
            object? targetCharacter,
            int? characterId,
            float damageValue,
            bool ignoreArmor,
            bool ignoreDifficulty,
            float critRate,
            float critDamageFactor,
            float armorPiercing)
        {
            Health = health;
            OriginalDamageInfo = originalDamageInfo;
            TargetGameObject = targetGameObject;
            TargetCharacter = targetCharacter;
            CharacterId = characterId;
            DamageValue = damageValue;
            IgnoreArmor = ignoreArmor;
            IgnoreDifficulty = ignoreDifficulty;
            CritRate = critRate;
            CritDamageFactor = critDamageFactor;
            ArmorPiercing = armorPiercing;
            CancelDamage = false;
        }
    }

    /// <summary>
    /// 伤害应用后事件
    /// 在伤害已应用后触发，用于统计、日志等
    /// </summary>
    public class AfterDamageAppliedEvent
    {
        /// <summary>
        /// Health 组件实例
        /// </summary>
        public object Health { get; }

        /// <summary>
        /// DamageInfo 对象
        /// </summary>
        public object DamageInfo { get; }

        /// <summary>
        /// 受伤角色的 GameObject
        /// </summary>
        public GameObject? TargetGameObject { get; }

        /// <summary>
        /// 受伤角色的 CharacterMainControl
        /// </summary>
        public object? TargetCharacter { get; }

        /// <summary>
        /// 角色 ID
        /// </summary>
        public int? CharacterId { get; }

        /// <summary>
        /// 是否是远程玩家
        /// </summary>
        public bool IsRemotePlayer { get; }

        /// <summary>
        /// 是否是本地玩家
        /// </summary>
        public bool IsLocalPlayer { get; }

        /// <summary>
        /// 实际造成的伤害值
        /// </summary>
        public float ActualDamage { get; }

        /// <summary>
        /// 剩余生命值
        /// </summary>
        public float RemainingHealth { get; }

        /// <summary>
        /// 是否导致死亡
        /// </summary>
        public bool CausedDeath { get; }

        public AfterDamageAppliedEvent(
            object health,
            object damageInfo,
            GameObject? targetGameObject,
            object? targetCharacter,
            int? characterId,
            bool isRemotePlayer,
            bool isLocalPlayer,
            float actualDamage,
            float remainingHealth,
            bool causedDeath)
        {
            Health = health;
            DamageInfo = damageInfo;
            TargetGameObject = targetGameObject;
            TargetCharacter = targetCharacter;
            CharacterId = characterId;
            IsRemotePlayer = isRemotePlayer;
            IsLocalPlayer = isLocalPlayer;
            ActualDamage = actualDamage;
            RemainingHealth = remainingHealth;
            CausedDeath = causedDeath;
        }
    }
}

