using System;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 单位创建补丁 - 拦截 CharacterSpawnerRoot.AddCreatedCharacter
    /// 捕获所有怪物/NPC 的创建事件并发布到 EventBus
    /// </summary>
    [HarmonyPatch]
    public static class CharacterCreationPatch
    {
        private static int _nextCharacterId = 1;
        private static readonly System.Collections.Generic.Dictionary<object, int> _characterIds 
            = new System.Collections.Generic.Dictionary<object, int>();

        /// <summary>
        /// 获取 CharacterSpawnerRoot 类型
        /// </summary>
        private static Type? GetCharacterSpawnerRootType()
        {
            return AccessTools.TypeByName("CharacterSpawnerRoot");
        }

        /// <summary>
        /// 动态指定要补丁的方法
        /// </summary>
        [HarmonyTargetMethod]
        static System.Reflection.MethodBase? TargetMethod()
        {
            var type = GetCharacterSpawnerRootType();
            if (type == null)
            {
                Debug.LogWarning("[CharacterCreationPatch] 找不到 CharacterSpawnerRoot 类型");
                return null;
            }

            // 查找 AddCreatedCharacter 方法
            var method = AccessTools.Method(type, "AddCreatedCharacter");
            if (method == null)
            {
                Debug.LogWarning("[CharacterCreationPatch] 找不到 CharacterSpawnerRoot.AddCreatedCharacter 方法");
                return null;
            }

            Debug.Log("[CharacterCreationPatch] ✅ 成功定位 CharacterSpawnerRoot.AddCreatedCharacter 方法");
            return method;
        }

        /// <summary>
        /// 后置补丁 - 在单位创建完成后触发
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(object c) // c 是 CharacterMainControl 参数
        {
            try
            {
                if (c == null) return;

                // 生成唯一 ID
                int characterId = _nextCharacterId++;
                _characterIds[c] = characterId;

                // 获取 GameObject
                GameObject? gameObject = null;
                if (c is Component component)
                {
                    gameObject = component.gameObject;
                }

                // 发布事件到 EventBus
                if (GameContext.IsInitialized)
                {
                    var evt = new CharacterSpawnedEvent(c, gameObject, characterId);
                    GameContext.Instance.EventBus.Publish(evt);
                    
                    #if DEBUG || UNITY_EDITOR
                    Debug.Log($"[CharacterCreationPatch] 单位已创建: ID={characterId}, Name={gameObject?.name ?? "Unknown"}");
                    #endif
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterCreationPatch] 处理单位创建失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 获取角色的 ID
        /// </summary>
        public static int GetCharacterId(object characterMainControl)
        {
            if (characterMainControl != null && _characterIds.TryGetValue(characterMainControl, out int id))
            {
                return id;
            }
            return -1;
        }

        /// <summary>
        /// 清理角色 ID 映射
        /// </summary>
        internal static void RemoveCharacterId(object characterMainControl)
        {
            if (characterMainControl != null)
            {
                _characterIds.Remove(characterMainControl);
            }
        }

        /// <summary>
        /// 清理所有数据
        /// </summary>
        public static void Clear()
        {
            _characterIds.Clear();
            _nextCharacterId = 1;
        }
    }
}

