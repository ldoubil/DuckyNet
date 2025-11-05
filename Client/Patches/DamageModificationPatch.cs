using HarmonyLib;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 伤害修改补丁
    /// 拦截 Health.Hurt() 方法，在伤害应用前触发事件，允许外部修改伤害参数
    /// </summary>
    [HarmonyPatch]
    public static class DamageModificationPatch
    {
        /// <summary>
        /// 动态指定要补丁的方法
        /// </summary>
        [HarmonyTargetMethod]
        static System.Reflection.MethodBase? TargetMethod()
        {
            var healthType = AccessTools.TypeByName("Health");
            if (healthType == null)
            {
                Debug.LogWarning("[DamageModificationPatch] 找不到 Health 类型");
                return null;
            }

            var hurtMethod = AccessTools.Method(healthType, "Hurt");
            if (hurtMethod == null)
            {
                Debug.LogWarning("[DamageModificationPatch] 找不到 Health.Hurt 方法");
                return null;
            }

            Debug.Log("[DamageModificationPatch] ✅ 成功定位 Health.Hurt 方法");
            return hurtMethod;
        }

        /// <summary>
        /// 前置补丁 - 在伤害计算前修改 DamageInfo
        /// </summary>
        [HarmonyPrefix]
        static bool Prefix(object __instance, ref object damageInfo, object[] __args)
        {
            try
            {
                // ===== 1. 获取目标角色信息 =====
                var healthType = __instance.GetType();
                var tryGetCharacterMethod = AccessTools.Method(healthType, "TryGetCharacter");
                object? character = tryGetCharacterMethod?.Invoke(__instance, null);

                GameObject? targetGameObject = null;
                int? characterId = null;

                if (character != null && character is Component component)
                {
                    targetGameObject = component.gameObject;
                }

                // ===== 2. 提取 DamageInfo 参数 =====
                var damageInfoType = damageInfo.GetType();
                
                var damageValueField = AccessTools.Field(damageInfoType, "damageValue");
                var ignoreArmorField = AccessTools.Field(damageInfoType, "ignoreArmor");
                var ignoreDifficultyField = AccessTools.Field(damageInfoType, "ignoreDifficulty");
                var critRateField = AccessTools.Field(damageInfoType, "critRate");
                var critDamageFactorField = AccessTools.Field(damageInfoType, "critDamageFactor");
                var armorPiercingField = AccessTools.Field(damageInfoType, "armorPiercing");

                if (damageValueField == null)
                {
                    Debug.LogWarning("[DamageModificationPatch] 无法找到 damageValue 字段");
                    return true; // 继续执行原方法
                }

                float originalDamageValue = (float)damageValueField.GetValue(damageInfo);
                bool ignoreArmor = ignoreArmorField != null && (bool)ignoreArmorField.GetValue(damageInfo);
                bool ignoreDifficulty = ignoreDifficultyField != null && (bool)ignoreDifficultyField.GetValue(damageInfo);
                float critRate = critRateField != null ? (float)critRateField.GetValue(damageInfo) : 0f;
                float critDamageFactor = critDamageFactorField != null ? (float)critDamageFactorField.GetValue(damageInfo) : 1f;
                float armorPiercing = armorPiercingField != null ? (float)armorPiercingField.GetValue(damageInfo) : 0f;

                // ===== 3. 创建并发布事件 =====
                var damageEvent = new BeforeDamageAppliedEvent(
                    health: __instance,
                    originalDamageInfo: damageInfo,
                    targetGameObject: targetGameObject,
                    targetCharacter: character,
                    characterId: characterId,
                    damageValue: originalDamageValue,
                    ignoreArmor: ignoreArmor,
                    ignoreDifficulty: ignoreDifficulty,
                    critRate: critRate,
                    critDamageFactor: critDamageFactor,
                    armorPiercing: armorPiercing
                );
                
                // 🔥 使用 GameContext 的 EventBus 实例（而不是 EventBus.Instance 单例）
                if (!GameContext.IsInitialized)
                {
                    return true;
                }
                
                var eventBus = GameContext.Instance.EventBus;
                eventBus.Publish(damageEvent);

                // ===== 4. 检查是否取消伤害 =====
                if (damageEvent.CancelDamage)
                {
                    return false;
                }

                // ===== 5. 应用修改后的参数 =====
                // 🔥 对于结构体，需要创建新实例并重新装箱
                bool modified = false;
                
                // 检查是否需要修改
                if (System.Math.Abs(damageEvent.DamageValue - originalDamageValue) > 0.001f)
                {
                    modified = true;
                }
                if (ignoreArmorField != null && damageEvent.IgnoreArmor != ignoreArmor)
                {
                    modified = true;
                }
                if (ignoreDifficultyField != null && damageEvent.IgnoreDifficulty != ignoreDifficulty)
                {
                    modified = true;
                }
                if (critRateField != null && System.Math.Abs(damageEvent.CritRate - critRate) > 0.001f)
                {
                    modified = true;
                }
                if (critDamageFactorField != null && System.Math.Abs(damageEvent.CritDamageFactor - critDamageFactor) > 0.001f)
                {
                    modified = true;
                }
                if (armorPiercingField != null && System.Math.Abs(damageEvent.ArmorPiercing - armorPiercing) > 0.001f)
                {
                    modified = true;
                }

                if (modified)
                {
                    // 创建新的结构体实例（复制所有字段）
                    object newDamageInfo = System.Activator.CreateInstance(damageInfoType);
                    
                    // 复制所有字段（包括修改和未修改的）
                    foreach (var field in damageInfoType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        object? value = field.GetValue(damageInfo);
                        field.SetValue(newDamageInfo, value);
                    }
                    
                    // 应用修改的字段
                    damageValueField.SetValue(newDamageInfo, damageEvent.DamageValue);
                    if (ignoreArmorField != null)
                        ignoreArmorField.SetValue(newDamageInfo, damageEvent.IgnoreArmor);
                    if (ignoreDifficultyField != null)
                        ignoreDifficultyField.SetValue(newDamageInfo, damageEvent.IgnoreDifficulty);
                    if (critRateField != null)
                        critRateField.SetValue(newDamageInfo, damageEvent.CritRate);
                    if (critDamageFactorField != null)
                        critDamageFactorField.SetValue(newDamageInfo, damageEvent.CritDamageFactor);
                    if (armorPiercingField != null)
                        armorPiercingField.SetValue(newDamageInfo, damageEvent.ArmorPiercing);
                    
                    // 🔥 同时修改 ref 参数和 __args 数组
                    damageInfo = newDamageInfo;
                    
                    // 🔥 关键：通过 __args 修改实际传递给方法的参数
                    if (__args != null && __args.Length > 0)
                    {
                        // 找到 DamageInfo 参数的位置
                        for (int i = 0; i < __args.Length; i++)
                        {
                            if (__args[i] != null && __args[i].GetType() == damageInfoType)
                            {
                                __args[i] = newDamageInfo;
                                break;
                            }
                        }
                    }
                }

                return true; // 继续执行原方法
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DamageModificationPatch] 处理伤害修改失败: {ex.Message}");
                Debug.LogException(ex);
                return true; // 发生错误时继续执行原方法，避免游戏崩溃
            }
        }

        /// <summary>
        /// 后置补丁 - 在伤害应用后触发事件
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(object __instance, object damageInfo)
        {
            try
            {
                // ===== 1. 获取目标角色信息 =====
                var healthType = __instance.GetType();
                var tryGetCharacterMethod = AccessTools.Method(healthType, "TryGetCharacter");
                object? character = tryGetCharacterMethod?.Invoke(__instance, null);

                GameObject? targetGameObject = null;
                int? characterId = null;
                bool isRemotePlayer = false;
                bool isLocalPlayer = false;

                if (character != null && character is Component component)
                {
                    targetGameObject = component.gameObject;
                    isRemotePlayer = targetGameObject.name.Contains("[RemotePlayer]");
                    isLocalPlayer = targetGameObject.name.Contains("[LocalPlayer]");
                }

                // ===== 2. 获取伤害值和剩余生命 =====
                var damageInfoType = damageInfo.GetType();
                var damageValueField = AccessTools.Field(damageInfoType, "damageValue");
                float actualDamage = damageValueField != null ? (float)damageValueField.GetValue(damageInfo) : 0f;

                var currentHealthProp = AccessTools.Property(healthType, "CurrentHealth");
                var maxHealthProp = AccessTools.Property(healthType, "MaxHealth");
                
                float remainingHealth = currentHealthProp != null ? (float)currentHealthProp.GetValue(__instance) : 0f;
                bool causedDeath = remainingHealth <= 0f;

                // ===== 3. 发布伤害应用后事件 =====
                var afterEvent = new AfterDamageAppliedEvent(
                    health: __instance,
                    damageInfo: damageInfo,
                    targetGameObject: targetGameObject,
                    targetCharacter: character,
                    characterId: characterId,
                    isRemotePlayer: isRemotePlayer,
                    isLocalPlayer: isLocalPlayer,
                    actualDamage: actualDamage,
                    remainingHealth: remainingHealth,
                    causedDeath: causedDeath
                );

                // 🔥 使用 GameContext 的 EventBus 实例
                if (GameContext.IsInitialized)
                {
                    GameContext.Instance.EventBus.Publish(afterEvent);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DamageModificationPatch] 发布伤害应用后事件失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }
    }
}

