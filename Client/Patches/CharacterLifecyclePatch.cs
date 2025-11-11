using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 角色生命周期补丁集合 - 统一管理角色的创建、销毁、死亡事件
    /// </summary>
    public static class CharacterLifecyclePatch
    {
        #region 共享反射缓存

        private static Type? _characterMainControlType;
        private static Type? _healthType;
        private static System.Reflection.MethodInfo? _tryGetCharacterMethod;
        private static System.Reflection.PropertyInfo? _isDeadProperty;
        private static System.Reflection.PropertyInfo? _isMainCharacterProperty;

        private static Type? CharacterMainControlType
        {
            get
            {
                if (_characterMainControlType == null)
                {
                    _characterMainControlType = AccessTools.TypeByName("CharacterMainControl");
                }
                return _characterMainControlType;
            }
        }

        #endregion

        #region 角色 ID 管理

        private static readonly Dictionary<object, string> _characterIds = new Dictionary<object, string>();

        /// <summary>
        /// 生成全局唯一的 NPC ID（使用 UUID）
        /// </summary>
        private static string GenerateNpcId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 获取角色的 ID
        /// </summary>
        public static string GetCharacterId(object characterMainControl)
        {
            if (characterMainControl != null && _characterIds.TryGetValue(characterMainControl, out string? id))
            {
                return id;
            }
            return "";
        }

        /// <summary>
        /// 清理角色 ID 映射
        /// </summary>
        private static void RemoveCharacterId(object characterMainControl)
        {
            _characterIds?.Remove(characterMainControl);
        }

        /// <summary>
        /// 清理所有数据
        /// </summary>
        public static void Clear()
        {
            _characterIds.Clear();
        }

        #endregion

        #region 角色创建 Patch

        /// <summary>
        /// 角色创建补丁 - 拦截 CharacterSpawnerRoot.AddCreatedCharacter
        /// </summary>
        [HarmonyPatch]
        public static class CreationPatch
        {
            [HarmonyTargetMethod]
            static System.Reflection.MethodBase? TargetMethod()
            {
                var type = AccessTools.TypeByName("CharacterSpawnerRoot");
                if (type == null)
                {
                    Debug.LogWarning("[CharacterLifecycle] 找不到 CharacterSpawnerRoot 类型");
                    return null;
                }

                var method = AccessTools.Method(type, "AddCreatedCharacter");
                if (method == null)
                {
                    Debug.LogWarning("[CharacterLifecycle] 找不到 AddCreatedCharacter 方法");
                    return null;
                }

                return method;
            }

            [HarmonyPostfix]
            static void Postfix(object c)
            {
                try
                {
                    if (c == null || !GameContext.IsInitialized) return;

                    // 生成全局唯一 ID（UUID）
                    string characterId = GenerateNpcId();
                    _characterIds[c] = characterId;

                    // 获取 GameObject
                    GameObject? gameObject = (c is Component component) ? component.gameObject : null;

                    // 发布事件
                    var evt = new CharacterSpawnedEvent(c, gameObject, characterId);
                    Debug.Log($"[CharacterLifecycle] 角色创建: {gameObject?.name} ID: {characterId}");
                    GameContext.Instance.EventBus.Publish(evt);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CharacterLifecycle] 角色创建失败: {ex.Message}");
                }
            }
        }

        #endregion

        #region 角色销毁 Patch

        /// <summary>
        /// 角色销毁补丁 - 拦截 CharacterMainControl.OnDestroy
        /// 使用 Unity 生命周期方法更可靠，无论通过何种方式销毁都会触发
        /// </summary>
        [HarmonyPatch]
        public static class DestructionPatch
        {
            [HarmonyTargetMethod]
            static System.Reflection.MethodBase? TargetMethod()
            {
                var type = CharacterMainControlType;
                if (type == null)
                {
                    Debug.LogWarning("[CharacterLifecycle] 找不到 CharacterMainControl 类型");
                    return null;
                }

                var method = AccessTools.Method(type, "OnDestroy");
                if (method == null)
                {
                    Debug.LogWarning("[CharacterLifecycle] 找不到 OnDestroy 方法");
                    return null;
                }

                return method;
            }

            [HarmonyPrefix]
            static void Prefix(object __instance)
            {
                try
                {
                    if (__instance == null || !GameContext.IsInitialized) return;

                    // 获取 GameObject
                    GameObject? gameObject = (__instance is Component component) ? component.gameObject : null;

                    // 获取角色 ID
                    string characterId = GetCharacterId(__instance);

                    // 发布销毁事件
                    var evt = new CharacterDestroyedEvent(__instance, gameObject, characterId);
                    GameContext.Instance.EventBus.Publish(evt);
                    RemoveCharacterId(__instance);
                    
                    Debug.Log($"[CharacterLifecycle] 角色销毁: {gameObject?.name} ID: {characterId}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CharacterLifecycle] 角色销毁事件失败: {ex.Message}");
                }
            }
        }

        #endregion

        #region 角色死亡 Patch

        /// <summary>
        /// 角色死亡补丁 - 拦截 Health.Hurt 方法
        /// </summary>
        [HarmonyPatch]
        public static class DeathPatch
        {
            [HarmonyTargetMethod]
            static System.Reflection.MethodBase? TargetMethod()
            {
                _healthType = AccessTools.TypeByName("Health");
                if (_healthType == null)
                {
                    Debug.LogWarning("[CharacterLifecycle] 找不到 Health 类型");
                    return null;
                }

                var method = AccessTools.Method(_healthType, "Hurt");
                if (method == null)
                {
                    Debug.LogWarning("[CharacterLifecycle] 找不到 Hurt 方法");
                    return null;
                }

                // 缓存反射成员
                _tryGetCharacterMethod = AccessTools.Method(_healthType, "TryGetCharacter");
                _isDeadProperty = AccessTools.Property(_healthType, "IsDead");

                if (CharacterMainControlType != null)
                {
                    _isMainCharacterProperty = AccessTools.Property(CharacterMainControlType, "IsMainCharacter");
                }

                return method;
            }

            [HarmonyPostfix]
            static void Postfix(object __instance, object damageInfo, bool __result)
            {
                try
                {
                    if (!__result || !GameContext.IsInitialized || _isDeadProperty == null) return;

                    // 检查是否死亡
                    bool isDead = (bool)(_isDeadProperty.GetValue(__instance) ?? false);
                    if (!isDead) return;

                    // 获取角色信息
                    object? characterMainControl = _tryGetCharacterMethod?.Invoke(__instance, null);
                    if (characterMainControl == null) return;

                    // 过滤掉玩家角色（只监听怪物/NPC 死亡）
                    if (_isMainCharacterProperty != null)
                    {
                        try
                        {
                            var isMainCharacter = (bool)(_isMainCharacterProperty.GetValue(characterMainControl) ?? false);
                            if (isMainCharacter) return;
                        }
                        catch
                        {
                            // 检查失败，继续处理
                        }
                    }

                    // 获取 GameObject 和 ID
                    GameObject? gameObject = (characterMainControl is Component component) ? component.gameObject : null;
                    string characterId = GetCharacterId(characterMainControl);

                    // 发布死亡事件
                    var evt = new CharacterDeathEvent(__instance, damageInfo, characterMainControl, gameObject, characterId);
                    GameContext.Instance.EventBus.Publish(evt);
                }
                catch
                {
                    // 静默失败，避免干扰游戏流程
                }
            }
        }

        #endregion
    }
}

