using System;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 血量事件桥接 - 直接 Patch Health 方法来监听血量变化
    /// </summary>
    public static class HealthEventBridge
    {
        private static Type? _healthType;
        private static Type? _characterMainControlType;
        private static System.Reflection.PropertyInfo? _currentHealthProperty;
        private static System.Reflection.PropertyInfo? _maxHealthProperty;
        private static System.Reflection.PropertyInfo? _isMainCharacterProperty;
        private static System.Reflection.PropertyInfo? _isDeadProperty;
        private static System.Reflection.MethodInfo? _tryGetCharacterMethod;

        /// <summary>
        /// 初始化反射缓存
        /// </summary>
        private static void InitializeReflectionCache()
        {
            if (_healthType != null) return;

            _healthType = AccessTools.TypeByName("Health");
            _characterMainControlType = AccessTools.TypeByName("CharacterMainControl");

            if (_healthType != null)
            {
                _currentHealthProperty = AccessTools.Property(_healthType, "CurrentHealth");
                _maxHealthProperty = AccessTools.Property(_healthType, "MaxHealth");
                _isDeadProperty = AccessTools.Property(_healthType, "IsDead");
                _tryGetCharacterMethod = AccessTools.Method(_healthType, "TryGetCharacter");
            }

            if (_characterMainControlType != null)
            {
                _isMainCharacterProperty = AccessTools.Property(_characterMainControlType, "IsMainCharacter");
            }
        }

        /// <summary>
        /// 检查是否是本地玩家
        /// </summary>
        private static bool IsLocalPlayer(object? characterMainControl)
        {
            if (characterMainControl == null || _isMainCharacterProperty == null)
                return false;

            try
            {
                return (bool)(_isMainCharacterProperty.GetValue(characterMainControl) ?? false);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取角色信息
        /// </summary>
        private static (GameObject?, object?) GetCharacterInfo(object healthInstance)
        {
            GameObject? gameObject = null;
            object? characterMainControl = null;

            if (_tryGetCharacterMethod != null)
            {
                characterMainControl = _tryGetCharacterMethod.Invoke(healthInstance, null);
                if (characterMainControl is Component component)
                {
                    gameObject = component.gameObject;
                }
            }

            return (gameObject, characterMainControl);
        }

        #region Health.CurrentHealth Setter Patch

        /// <summary>
        /// 监听血量变化 - Patch CurrentHealth 属性的 Setter
        /// </summary>
        [HarmonyPatch]
        public static class CurrentHealthPatch
        {
            [HarmonyTargetMethod]
            static System.Reflection.MethodBase? TargetMethod()
            {
                InitializeReflectionCache();

                if (_healthType == null)
                {
                    Debug.LogWarning("[HealthEventBridge] 找不到 Health 类型");
                    return null;
                }

                var property = AccessTools.Property(_healthType, "CurrentHealth");
                if (property == null)
                {
                    Debug.LogWarning("[HealthEventBridge] 找不到 Health.CurrentHealth 属性");
                    return null;
                }

                return property.GetSetMethod();
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    if (!GameContext.IsInitialized) return;

                    var (gameObject, character) = GetCharacterInfo(__instance);
                    bool isLocalPlayer = IsLocalPlayer(character);

                    float currentHealth = (float)(_currentHealthProperty?.GetValue(__instance) ?? 0f);
                    float maxHealth = (float)(_maxHealthProperty?.GetValue(__instance) ?? 0f);

                    var evt = new HealthChangedEvent(
                        __instance,
                        gameObject,
                        character,
                        isLocalPlayer,
                        currentHealth,
                        maxHealth);

                    GameContext.Instance.EventBus.Publish(evt);

                    #if DEBUG || UNITY_EDITOR
                    if (isLocalPlayer)
                    {
                        Debug.Log($"[HealthEventBridge] 💚 血量变化: {currentHealth:F0}/{maxHealth:F0}");
                    }
                    #endif
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[HealthEventBridge] CurrentHealth Setter 失败: {ex.Message}");
                }
            }
        }

        #endregion

        #region Health.MaxHealth Setter Patch

        /// <summary>
        /// 监听最大血量变化 - Patch MaxHealth 属性的 Setter
        /// </summary>
        [HarmonyPatch]
        public static class MaxHealthPatch
        {
            [HarmonyTargetMethod]
            static System.Reflection.MethodBase? TargetMethod()
            {
                InitializeReflectionCache();

                if (_healthType == null)
                {
                    Debug.LogWarning("[HealthEventBridge] 找不到 Health 类型");
                    return null;
                }

                var property = AccessTools.Property(_healthType, "MaxHealth");
                if (property == null)
                {
                    Debug.LogWarning("[HealthEventBridge] 找不到 Health.MaxHealth 属性");
                    return null;
                }

                return property.GetSetMethod();
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    if (!GameContext.IsInitialized) return;

                    var (gameObject, character) = GetCharacterInfo(__instance);
                    bool isLocalPlayer = IsLocalPlayer(character);

                    float maxHealth = (float)(_maxHealthProperty?.GetValue(__instance) ?? 0f);

                    var evt = new MaxHealthChangedEvent(
                        __instance,
                        gameObject,
                        character,
                        isLocalPlayer,
                        maxHealth);

                    GameContext.Instance.EventBus.Publish(evt);

                    #if DEBUG || UNITY_EDITOR
                    if (isLocalPlayer)
                    {
                        Debug.Log($"[HealthEventBridge] 💪 最大血量变化: {maxHealth:F0}");
                    }
                    #endif
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[HealthEventBridge] MaxHealth Setter 失败: {ex.Message}");
                }
            }
        }

        #endregion

        #region Health.Hurt Method Patch

        /// <summary>
        /// 监听受伤 - Patch Hurt 方法
        /// </summary>
        [HarmonyPatch]
        public static class HurtPatch
        {
            [HarmonyTargetMethod]
            static System.Reflection.MethodBase? TargetMethod()
            {
                InitializeReflectionCache();

                if (_healthType == null)
                {
                    Debug.LogWarning("[HealthEventBridge] 找不到 Health 类型");
                    return null;
                }

                var method = AccessTools.Method(_healthType, "Hurt");
                if (method == null)
                {
                    Debug.LogWarning("[HealthEventBridge] 找不到 Health.Hurt 方法");
                    return null;
                }

                return method;
            }

            [HarmonyPostfix]
            static void Postfix(object __instance, object damageInfo, bool __result)
            {
                try
                {
                    // 如果没有造成伤害，直接返回
                    if (!__result || !GameContext.IsInitialized) return;

                    var (gameObject, character) = GetCharacterInfo(__instance);
                    bool isLocalPlayer = IsLocalPlayer(character);

                    float currentHealth = (float)(_currentHealthProperty?.GetValue(__instance) ?? 0f);
                    float maxHealth = (float)(_maxHealthProperty?.GetValue(__instance) ?? 0f);

                    // 发布受伤事件
                    var hurtEvt = new CharacterHurtEvent(
                        __instance,
                        damageInfo,
                        gameObject,
                        character,
                        isLocalPlayer,
                        currentHealth,
                        maxHealth);

                    GameContext.Instance.EventBus.Publish(hurtEvt);

                    #if DEBUG || UNITY_EDITOR
                    if (isLocalPlayer)
                    {
                        Debug.Log($"[HealthEventBridge] 🩸 角色受伤: 剩余血量 {currentHealth:F0}/{maxHealth:F0}");
                    }
                    #endif

                    // 检查是否死亡
                    bool isDead = (bool)(_isDeadProperty?.GetValue(__instance) ?? false);
                    if (isDead)
                    {
                        var deadEvt = new CharacterDeadEvent(
                            __instance,
                            damageInfo,
                            gameObject,
                            character,
                            isLocalPlayer);

                        GameContext.Instance.EventBus.Publish(deadEvt);

                        #if DEBUG || UNITY_EDITOR
                        if (isLocalPlayer)
                        {
                            Debug.Log($"[HealthEventBridge] 💀 本地玩家死亡");
                        }
                        #endif
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[HealthEventBridge] Hurt Postfix 失败: {ex.Message}");
                }
            }
        }

        #endregion
    }
}

