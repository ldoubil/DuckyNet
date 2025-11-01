using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.Core.Utils;
using DuckyNet.Client.Core.Helpers;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 单位管理器 - 专注于创建远程玩家角色
    /// 
    /// 远程玩家创建流程（对标 LevelManager.CreateMainCharacterAsync）：
    /// 1. 创建新的 Item 实例（不从存档加载）
    /// 2. 获取角色模型预制体
    /// 3. 创建角色实例（调用 CharacterCreator.CreateCharacter）
    /// 4. 配置角色（队伍为 Teams.player，初始化血量）
    /// 5. 添加到管理列表跟踪生命周期
    /// 6. 发布创建完成事件
    /// </summary>
    public class UnitManager : IDisposable
    {
        private readonly List<GameObject> _managedRemotePlayers = new List<GameObject>();
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();

        /// <summary>
        /// 获取当前管理的远程玩家数量
        /// </summary>
        public int RemotePlayerCount => _managedRemotePlayers.Count;

        /// <summary>
        /// 获取所有管理的远程玩家
        /// </summary>
        public IReadOnlyList<GameObject> ManagedRemotePlayers => _managedRemotePlayers.AsReadOnly();

        public UnitManager()
        {
            // 延迟订阅事件（等待 GameContext 初始化）
            if (GameContext.IsInitialized)
            {
                SubscribeToEvents();
            }
        }

        /// <summary>
        /// 订阅 EventBus 事件
        /// </summary>
        private void SubscribeToEvents()
        {
            _eventSubscriber.Subscribe<CreateRemoteCharacterRequestEvent>(OnCreateRemoteCharacterRequested);
            _eventSubscriber.EnsureInitializedAndSubscribe();
            UnityEngine.Debug.Log("[UnitManager] 已订阅远程玩家创建事件");
        }

        /// <summary>
        /// 处理创建远程玩家请求
        /// </summary>
        private void OnCreateRemoteCharacterRequested(CreateRemoteCharacterRequestEvent evt)
        {
            if (string.IsNullOrEmpty(evt.PlayerId))
            {
                UnityEngine.Debug.LogWarning("[UnitManager] 远程玩家创建请求：PlayerId 为空");
                PublishCharacterCreated(evt.PlayerId, null);
                return;
            }

            try
            {
                UnityEngine.Debug.Log($"[UnitManager] 创建远程玩家: {evt.PlayerId}");
                
                var character = CreateRemotePlayer(
                    playerId: evt.PlayerId,
                    position: Vector3.zero
                );

                if (character != null)
                {
                    UnityEngine.Debug.Log($"[UnitManager] ✅ 远程玩家创建成功: {evt.PlayerId}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[UnitManager] 远程玩家创建失败: {evt.PlayerId}");
                }

                PublishCharacterCreated(evt.PlayerId, character);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UnitManager] 创建远程玩家异常: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
                PublishCharacterCreated(evt.PlayerId, null);
            }
        }

        /// <summary>
        /// 发布远程玩家创建完成事件
        /// </summary>
        private void PublishCharacterCreated(string playerId, GameObject? character)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteCharacterCreatedEvent(playerId, character));
            }
        }

        /// <summary>
        /// 创建远程玩家
        /// 对标 LevelManager.CreateMainCharacterAsync() 的实现模式
        /// </summary>
        public GameObject? CreateRemotePlayer(string playerId, Vector3 position)
        {
            try
            {
                // 1. 创建新的 Item 实例（远程玩家总是创建新的，不从存档加载）
                var characterItem = CharacterCreationUtils.CreateCharacterItem();
                if (characterItem == null)
                {
                    UnityEngine.Debug.LogError("[UnitManager] 创建 Item 实例失败");
                    return null;
                }

                // 2. 获取角色模型预制体
                var modelPrefab = CharacterCreationUtils.GetCharacterModelPrefab();
                if (modelPrefab == null)
                {
                    UnityEngine.Debug.LogError("[UnitManager] 获取角色模型失败");
                    return null;
                }

                // 3. 创建角色实例（调用 CharacterCreator.CreateCharacter）
                var newCharacter = CharacterCreationUtils.CreateCharacterInstance(
                    characterItem,
                    modelPrefab,
                    position,
                    Quaternion.identity
                );

                if (newCharacter == null)
                {
                    UnityEngine.Debug.LogError("[UnitManager] 创建角色实例失败");
                    return null;
                }

                // 4. 配置角色（名称、位置、队伍=player、初始化血量）
                if (!CharacterCreationUtils.ConfigureCharacter(newCharacter, $"RemotePlayer_{playerId}", position, team: 0))
                {
                    UnityEngine.Debug.LogError("[UnitManager] 配置角色失败");
                    return null;
                }

                // 4.5 配置角色名字显示（必须在请求血条之前）
                ConfigureCharacterName(newCharacter, playerId);

                // 4.6 禁用物理组件防止下落
                DisablePhysics(newCharacter);

                // 5. 添加到管理列表
                Component? characterComponent = newCharacter as Component;
                if (characterComponent != null)
                {
                    _managedRemotePlayers.Add(characterComponent.gameObject);
                    UnityEngine.Debug.Log($"[UnitManager] 远程玩家已加入管理列表，当前总数: {_managedRemotePlayers.Count}");
                }

                return characterComponent?.gameObject;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UnitManager] 创建远程玩家异常: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
                return null;
            }
        }

        /// <summary>
        /// 禁用物理组件
        /// </summary>
        private void DisablePhysics(object character)
        {
            try
            {
                if (character is Component component)
                {
                    var rigidbody = component.gameObject.GetComponent<Rigidbody>();
                    if (rigidbody != null)
                    {
                        rigidbody.isKinematic = true;
                        rigidbody.detectCollisions = false;
                        rigidbody.useGravity = false;
                        UnityEngine.Debug.Log($"[UnitManager] 已禁用物理组件: {component.gameObject.name}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[UnitManager] 禁用物理组件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 配置角色名字显示
        /// </summary>
        private void ConfigureCharacterName(object character, string playerId)
        {
            try
            {
                // 获取 CharacterMainControl 组件
                var charType = character.GetType();
                
                // 创建新的 CharacterRandomPreset
                var presetType = HarmonyLib.AccessTools.TypeByName("CharacterRandomPreset");
                if (presetType == null)
                {
                    UnityEngine.Debug.LogWarning("[UnitManager] 无法找到 CharacterRandomPreset 类型");
                    return;
                }

                object? preset = UnityEngine.ScriptableObject.CreateInstance(presetType);
                if (preset == null) return;

                // 设置 showName = true
                var showNameProp = HarmonyLib.AccessTools.Property(presetType, "showName");
                if (showNameProp != null && showNameProp.CanWrite)
                {
                    showNameProp.SetValue(preset, true);
                }

                // 设置 nameKey = playerId
                var nameKeyProp = HarmonyLib.AccessTools.Property(presetType, "nameKey");
                if (nameKeyProp != null && nameKeyProp.CanWrite)
                {
                    nameKeyProp.SetValue(preset, playerId);
                }

                // 将预设赋值给角色
                var characterPresetProp = HarmonyLib.AccessTools.Property(charType, "characterPreset");
                if (characterPresetProp != null && characterPresetProp.CanWrite)
                {
                    characterPresetProp.SetValue(character, preset);
                }

                // 刷新血条显示
                var healthProp = HarmonyLib.AccessTools.Property(charType, "Health");
                object? health = healthProp?.GetValue(character);
                if (health != null)
                {
                    var requestMethod = HarmonyLib.AccessTools.Method(health.GetType(), "RequestHealthBar", Type.EmptyTypes);
                    requestMethod?.Invoke(health, null);
                }

                UnityEngine.Debug.Log($"[UnitManager] 已配置远程玩家名字显示: {playerId}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[UnitManager] 配置远程玩家名字显示失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 销毁远程玩家
        /// </summary>
        public bool DestroyRemotePlayer(GameObject player)
        {
            if (player == null) return false;

            if (_managedRemotePlayers.Remove(player))
            {
                UnityEngine.Object.Destroy(player);
                UnityEngine.Debug.Log($"[UnitManager] 远程玩家已销毁，剩余数量: {_managedRemotePlayers.Count}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 销毁所有远程玩家
        /// </summary>
        public void DestroyAllRemotePlayers()
        {
            foreach (var player in _managedRemotePlayers)
            {
                if (player != null)
                {
                    UnityEngine.Object.Destroy(player);
                }
            }
            _managedRemotePlayers.Clear();
            UnityEngine.Debug.Log("[UnitManager] 所有远程玩家已销毁");
        }

        /// <summary>
        /// 确保已订阅事件（用于延迟初始化场景）
        /// </summary>
        public void EnsureSubscribed()
        {
            _eventSubscriber.EnsureInitializedAndSubscribe();
        }

        public void Dispose()
        {
            _eventSubscriber?.Dispose();
            DestroyAllRemotePlayers();
        }
    }
}


