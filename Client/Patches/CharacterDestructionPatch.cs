using System;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 单位销毁补丁 - 拦截 Object.Destroy
    /// 捕获 CharacterMainControl 的销毁事件并发布到 EventBus
    /// </summary>
    [HarmonyPatch(typeof(UnityEngine.Object), nameof(UnityEngine.Object.Destroy), new Type[] { typeof(UnityEngine.Object) })]
    public static class CharacterDestructionPatch
    {
        /// <summary>
        /// 获取 CharacterMainControl 类型
        /// </summary>
        private static Type? _characterMainControlType;
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

        /// <summary>
        /// 前置补丁 - 在对象被销毁前捕获
        /// </summary>
        [HarmonyPrefix]
        static void Prefix(UnityEngine.Object obj)
        {
            try
            {
                if (obj == null || CharacterMainControlType == null) return;

                // 检查是否是 GameObject
                GameObject? gameObject = null;
                object? characterMainControl = null;

                if (obj is GameObject go)
                {
                    gameObject = go;
                    // 尝试获取 CharacterMainControl 组件
                    var component = go.GetComponent(CharacterMainControlType);
                    if (component != null)
                    {
                        characterMainControl = component;
                    }
                }
                else if (CharacterMainControlType.IsInstanceOfType(obj))
                {
                    // 直接销毁 CharacterMainControl 组件
                    characterMainControl = obj;
                    if (obj is Component component)
                    {
                        gameObject = component.gameObject;
                    }
                }

                // 如果找到了 CharacterMainControl，发布事件
                if (characterMainControl != null && GameContext.IsInitialized)
                {
                    int characterId = CharacterCreationPatch.GetCharacterId(characterMainControl);
                    
                    var evt = new CharacterDestroyedEvent(characterMainControl, gameObject, characterId);
                    GameContext.Instance.EventBus.Publish(evt);

                    #if DEBUG || UNITY_EDITOR
                    Debug.Log($"[CharacterDestructionPatch] 单位即将销毁: ID={characterId}, Name={gameObject?.name ?? "Unknown"}");
                    #endif

                    // 清理 ID 映射
                    CharacterCreationPatch.RemoveCharacterId(characterMainControl);
                }
            }
            catch (Exception ex)
            {
                // 静默处理异常，避免干扰正常的销毁流程
                #if DEBUG || UNITY_EDITOR
                Debug.LogWarning($"[CharacterDestructionPatch] 处理单位销毁失败: {ex.Message}");
                #endif
            }
        }
    }
}

