using System;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 单位死亡补丁 - 使用 Harmony 拦截 Health.Hurt 方法
    /// 比反射订阅事件更简单、更可靠
    /// </summary>
    [HarmonyPatch]
    public static class CharacterDeathPatch
    {
        private static Type? _healthType;
        private static System.Reflection.MethodInfo? _tryGetCharacterMethod;
        private static System.Reflection.PropertyInfo? _isDeadProperty;
        private static System.Reflection.PropertyInfo? _isMainCharacterProperty;

        /// <summary>
        /// 动态指定要补丁的方法
        /// </summary>
        [HarmonyTargetMethod]
        static System.Reflection.MethodBase? TargetMethod()
        {
            _healthType = AccessTools.TypeByName("Health");
            if (_healthType == null)
            {
                Debug.LogWarning("[CharacterDeathPatch] 找不到 Health 类型");
                return null;
            }

            // 获取 Hurt 方法
            var method = AccessTools.Method(_healthType, "Hurt");
            if (method == null)
            {
                Debug.LogWarning("[CharacterDeathPatch] 找不到 Health.Hurt 方法");
                return null;
            }

            // 缓存常用的方法和属性
            _tryGetCharacterMethod = AccessTools.Method(_healthType, "TryGetCharacter");
            _isDeadProperty = AccessTools.Property(_healthType, "IsDead");

            // 获取 IsMainCharacter 属性
            var characterMainControlType = AccessTools.TypeByName("CharacterMainControl");
            if (characterMainControlType != null)
            {
                _isMainCharacterProperty = AccessTools.Property(characterMainControlType, "IsMainCharacter");
            }

            return method;
        }

        /// <summary>
        /// 后置补丁 - 在 Hurt 方法执行后检查是否死亡
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(object __instance, object damageInfo, bool __result)
        {
            try
            {
                // 如果没有造成伤害或游戏上下文未初始化，直接返回
                if (!__result || !GameContext.IsInitialized) return;

                // 检查是否死亡
                if (_isDeadProperty == null) return;
                bool isDead = (bool)(_isDeadProperty.GetValue(__instance) ?? false);
                if (!isDead) return;

                // 获取 CharacterMainControl
                object? characterMainControl = null;
                GameObject? gameObject = null;

                if (_tryGetCharacterMethod != null)
                {
                    characterMainControl = _tryGetCharacterMethod.Invoke(__instance, null);
                    
                    if (characterMainControl != null)
                    {
                        // 过滤掉玩家角色（只监听怪物/NPC 死亡）
                        if (_isMainCharacterProperty != null)
                        {
                            try
                            {
                                var isMainCharacter = (bool)(_isMainCharacterProperty.GetValue(characterMainControl) ?? false);
                                if (isMainCharacter)
                                {
                                    // 跳过玩家死亡事件
                                    return;
                                }
                            }
                            catch
                            {
                                // 如果检查失败，继续处理（保守策略）
                            }
                        }

                        if (characterMainControl is Component component)
                        {
                            gameObject = component.gameObject;
                        }
                    }
                }

                // 获取角色ID（保证与创建/销毁事件使用相同ID）
                int characterId = -1;
                if (characterMainControl != null)
                {
                    characterId = CharacterCreationPatch.GetCharacterId(characterMainControl);
                }

                // 发布死亡事件到 EventBus
                var evt = new CharacterDeathEvent(__instance, damageInfo, characterMainControl, gameObject, characterId);
                GameContext.Instance.EventBus.Publish(evt);

                #if DEBUG || UNITY_EDITOR
                Debug.Log($"[CharacterDeathPatch] 💀 单位死亡: ID={characterId}, Name={gameObject?.name ?? "Unknown"}");
                #endif
            }
            catch (Exception ex)
            {
                // 静默处理异常，避免干扰游戏流程
                #if DEBUG || UNITY_EDITOR
                Debug.LogWarning($"[CharacterDeathPatch] 处理死亡事件失败: {ex.Message}");
                #endif
            }
        }
    }
}

