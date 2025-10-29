using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using UnityEngine;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 角色同步客户端服务实现
    /// 接收服务器推送的其他玩家状态并应用
    /// </summary>
    public class CharacterSyncClientServiceImpl : ICharacterSyncClientService
    {
        // 远程角色缓存 <PlayerId, GameObject>
        private readonly Dictionary<string, GameObject> _remoteCharacters 
            = new Dictionary<string, GameObject>();

        // 同步数据缓存（用于插值）
        private readonly Dictionary<string, CharacterSyncData> _lastSyncData 
            = new Dictionary<string, CharacterSyncData>();
        
        // AttackTriggerCount 缓存（用于检测变化）
        private readonly Dictionary<string, int> _lastAttackTriggerCount 
            = new Dictionary<string, int>();

        public CharacterSyncClientServiceImpl()
        {
            Debug.Log("[CharacterSyncClient] 服务已创建");
        }

        /// <summary>
        /// 接收其他角色的状态更新
        /// </summary>
        public void OnCharacterStateUpdate(CharacterSyncData syncData)
        {
            try
            {
                if (syncData == null || string.IsNullOrEmpty(syncData.PlayerId))
                {
                    Debug.LogWarning("[CharacterSyncClient] 无效的同步数据");
                    return;
                }

                // 缓存最新数据
                _lastSyncData[syncData.PlayerId] = syncData;

                // 获取或创建远程角色
                var character = GetOrCreateRemoteCharacter(syncData.PlayerId);
                if (character != null)
                {
                    // 检查 Attack Trigger 变化
                    if (_lastAttackTriggerCount.TryGetValue(syncData.PlayerId, out int lastCount))
                    {
                        if (syncData.AttackTriggerCount > lastCount)
                        {
                            // Attack Trigger 计数增加，触发攻击动画
                            var animator = character.GetComponentInChildren<Animator>();
                            if (animator != null)
                            {
                                try
                                {
                                    animator.SetTrigger("Attack");
                                    Debug.Log($"[CharacterSyncClient] 触发攻击动画: {syncData.PlayerId}");
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[CharacterSyncClient] 触发攻击失败: {ex.Message}");
                                }
                            }
                        }
                    }
                    _lastAttackTriggerCount[syncData.PlayerId] = syncData.AttackTriggerCount;
                    
                    // 应用同步数据（使用插值）
                    syncData.ApplyToUnity(character, interpolate: true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterSyncClient] 处理状态更新失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 接收完整场景状态
        /// </summary>
        public void OnFullStateUpdate(CharacterSyncData[] allStates)
        {
            try
            {
                Debug.Log($"[CharacterSyncClient] 接收完整状态: {allStates.Length} 个角色");

                foreach (var syncData in allStates)
                {
                    if (syncData != null && !string.IsNullOrEmpty(syncData.PlayerId))
                    {
                        _lastSyncData[syncData.PlayerId] = syncData;

                        var character = GetOrCreateRemoteCharacter(syncData.PlayerId);
                        if (character != null)
                        {
                            // 初始状态不插值，直接应用
                            syncData.ApplyToUnity(character, interpolate: false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterSyncClient] 处理完整状态失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 角色离开通知
        /// </summary>
        public void OnCharacterLeft(string playerId)
        {
            try
            {
                Debug.Log($"[CharacterSyncClient] 角色离开: {playerId}");

                // 移除并销毁远程角色
                if (_remoteCharacters.TryGetValue(playerId, out var character))
                {
                    _remoteCharacters.Remove(playerId);
                    _lastSyncData.Remove(playerId);

                    if (character != null)
                    {
                        UnityEngine.Object.Destroy(character);
                        Debug.Log($"[CharacterSyncClient] 已销毁远程角色: {playerId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterSyncClient] 处理角色离开失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取或创建远程角色
        /// </summary>
        private GameObject GetOrCreateRemoteCharacter(string playerId)
        {
            // 如果已存在，直接返回
            if (_remoteCharacters.TryGetValue(playerId, out var existing) && existing != null)
            {
                return existing;
            }

            // 创建新的远程角色
            try
            {
                Debug.Log($"[CharacterSyncClient] 为玩家 {playerId} 创建远程角色");

                // 使用 UnitManager 创建角色
                var unitManager = Core.GameContext.Instance?.UnitManager;
                if (unitManager != null)
                {
                    // 创建远程玩家角色（team=1, 默认属性）
                    var character = unitManager.CreateUnit(playerId, Vector3.zero, team: 1, stats: new Core.UnitStats());
                    if (character != null)
                    {
                        // 标记为远程角色（禁用本地控制）
                        character.name = $"RemotePlayer_{playerId}";
                        
                        // 禁用可能的输入控制组件
                        DisableLocalControl(character);

                        _remoteCharacters[playerId] = character;
                        Debug.Log($"[CharacterSyncClient] ✅ 远程角色创建成功: {playerId}");
                        return character;
                    }
                }

                Debug.LogWarning($"[CharacterSyncClient] 无法创建远程角色: {playerId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterSyncClient] 创建远程角色失败: {ex.Message}");
                Debug.LogException(ex);
            }

            return null!;
        }

        /// <summary>
        /// 禁用本地控制（远程角色不需要本地输入）
        /// </summary>
        private void DisableLocalControl(GameObject character)
        {
            try
            {
                // 禁用动画控制脚本（重要！防止覆盖同步的动画参数）
                if (!Core.Debug.AnimatorFixer.DisableAnimationControl(character))
                {
                    Debug.LogWarning($"[CharacterSyncClient] 无法禁用动画控制: {character.name}");
                }
                else
                {
                    Debug.Log($"[CharacterSyncClient] ✅ 已禁用动画控制: {character.name}");
                }

                // 禁用可能的输入控制组件
                // 注意：这取决于游戏的实际结构
                var components = character.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    var typeName = component.GetType().Name;
                    // 禁用输入相关组件
                    if (typeName.Contains("Input") || 
                        typeName.Contains("Controller") || 
                        typeName.Contains("PlayerControl"))
                    {
                        component.enabled = false;
                        Debug.Log($"[CharacterSyncClient] 禁用组件: {typeName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterSyncClient] 禁用本地控制失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理所有远程角色
        /// </summary>
        public void CleanupAll()
        {
            Debug.Log("[CharacterSyncClient] 清理所有远程角色");

            foreach (var kvp in _remoteCharacters)
            {
                if (kvp.Value != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }

            _remoteCharacters.Clear();
            _lastSyncData.Clear();
        }

        /// <summary>
        /// 获取所有远程角色（只读）
        /// </summary>
        public IReadOnlyDictionary<string, GameObject> GetRemoteCharacters()
        {
            return _remoteCharacters;
        }
    }
}

