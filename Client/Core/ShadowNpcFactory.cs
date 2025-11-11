using System;
using UnityEngine;
using HarmonyLib;
using DuckyNet.Shared.Data;
using DuckyNet.Client.Core.Utils;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 影子 NPC 工厂 - 创建无 AI 的远程 NPC
    /// 
    /// 设计理念：
    /// 1. 使用与远程玩家相同的创建方式（CharacterCreationUtils）
    /// 2. 禁用所有 AI 组件（CharacterAI、NavMeshAgent）
    /// 3. 网络控制位置和旋转
    /// 4. 不参与游戏逻辑（设置为中立队伍）
    /// </summary>
    public static class ShadowNpcFactory
    {
        private static Type? _characterMainControlType;
        private static Type? _characterAIType;
        private static Type? _navMeshAgentType;
        private static bool _initialized = false;

        /// <summary>
        /// 初始化反射
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            _characterMainControlType = AccessTools.TypeByName("CharacterMainControl");
            _characterAIType = AccessTools.TypeByName("CharacterAI");
            _navMeshAgentType = Type.GetType("UnityEngine.AI.NavMeshAgent, UnityEngine.AIModule");

            _initialized = true;
            Debug.Log("[ShadowNpcFactory] 影子 NPC 工厂已初始化");
        }

        /// <summary>
        /// 创建影子 NPC（使用与远程玩家相同的方式）
        /// </summary>
        public static object? CreateShadowNpc(NpcSpawnData data)
        {
            try
            {
                if (!_initialized) Initialize();

                Debug.Log($"[ShadowNpcFactory] 开始创建影子 NPC: {data.NpcType}");

                // 1. 创建角色物品（CharacterItem）
                var characterItem = CharacterCreationUtils.CreateCharacterItem();
                if (characterItem == null)
                {
                    Debug.LogError("[ShadowNpcFactory] 创建角色物品失败");
                    return null;
                }

                // 2. 获取角色模型预制体
                var modelPrefab = CharacterCreationUtils.GetCharacterModelPrefab();
                if (modelPrefab == null)
                {
                    Debug.LogError("[ShadowNpcFactory] 获取模型预制体失败");
                    return null;
                }

                // 3. 创建角色实例
                Vector3 position = new Vector3(data.PositionX, data.PositionY, data.PositionZ);
                Quaternion rotation = Quaternion.Euler(0, data.RotationY, 0);
                
                var character = CharacterCreationUtils.CreateCharacterInstance(
                    characterItem, 
                    modelPrefab, 
                    position, 
                    rotation
                );

                if (character == null)
                {
                    Debug.LogError("[ShadowNpcFactory] 创建角色实例失败");
                    return null;
                }

                // 4. 配置角色（名称、位置、队伍=中立）
                CharacterCreationUtils.ConfigureCharacter(
                    character, 
                    $"RemoteNPC_{data.NpcType}", 
                    position, 
                    2 // team=2 (middle/中立)
                );

                // 5. 禁用 AI 组件
                if (character is Component component)
                {
                    DisableAIComponents(component.gameObject);
                    
                    // 添加标记组件
                    var marker = component.gameObject.AddComponent<ShadowNpcMarker>();
                    marker.NpcId = data.NpcId;
                    marker.NpcType = data.NpcType;
                    marker.SceneName = data.SceneName;
                    marker.SubSceneName = data.SubSceneName;
                }

                Debug.Log($"[ShadowNpcFactory] ✅ 影子 NPC 已创建: {data.NpcType} (ID: {data.NpcId})");

                return character;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShadowNpcFactory] 创建影子 NPC 失败: {ex.Message}");
                Debug.LogException(ex);
                return null;
            }
        }

        /// <summary>
        /// 更新影子 NPC 的位置和旋转
        /// </summary>
        public static void UpdateShadowNpcTransform(GameObject shadowNpc, Vector3 position, float rotationY)
        {
            if (shadowNpc == null) return;

            shadowNpc.transform.position = position;
            shadowNpc.transform.rotation = Quaternion.Euler(0, rotationY, 0);
        }

        /// <summary>
        /// 销毁影子 NPC
        /// </summary>
        public static void DestroyShadowNpc(GameObject shadowNpc)
        {
            if (shadowNpc != null)
            {
                UnityEngine.Object.Destroy(shadowNpc);
                Debug.Log($"[ShadowNpcFactory] 影子 NPC 已销毁: {shadowNpc.name}");
            }
        }

        /// <summary>
        /// 禁用 AI 组件
        /// </summary>
        private static void DisableAIComponents(GameObject npc)
        {
            try
            {
                int disabledCount = 0;

                // 禁用 CharacterAI
                if (_characterAIType != null)
                {
                    var ai = npc.GetComponentInChildren(_characterAIType) as MonoBehaviour;
                    if (ai != null)
                    {
                        ai.enabled = false;
                        disabledCount++;
                        Debug.Log($"[ShadowNpcFactory] ✅ 已禁用 CharacterAI");
                    }
                }

                // 禁用 NavMeshAgent
                if (_navMeshAgentType != null)
                {
                    var agent = npc.GetComponent(_navMeshAgentType) as MonoBehaviour;
                    if (agent != null)
                    {
                        agent.enabled = false;
                        disabledCount++;
                        Debug.Log($"[ShadowNpcFactory] ✅ 已禁用 NavMeshAgent");
                    }
                }

                // 禁用动画控制脚本（避免本地动画逻辑）
                var animControlType = AccessTools.TypeByName("CharacterAnimationControl");
                if (animControlType != null)
                {
                    var animControl = npc.GetComponentInChildren(animControlType) as MonoBehaviour;
                    if (animControl != null)
                    {
                        animControl.enabled = false;
                        disabledCount++;
                    }
                }

                Debug.Log($"[ShadowNpcFactory] AI 组件已禁用: {npc.name}, 共 {disabledCount} 个组件");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShadowNpcFactory] 禁用 AI 组件失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 影子 NPC 标记组件 - 用于识别和查找影子 NPC
    /// </summary>
    public class ShadowNpcMarker : MonoBehaviour
    {
        public string NpcId { get; set; } = "";
        public string NpcType { get; set; } = "";
        public string SceneName { get; set; } = "";
        public string SubSceneName { get; set; } = "";
    }
}

