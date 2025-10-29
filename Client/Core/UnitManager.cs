using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using System.Reflection;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 单位管理器 - 负责创建、销毁和管理游戏中的测试单位
    /// </summary>
    public class UnitManager : IDisposable
    {
        private readonly List<GameObject> _managedUnits = new List<GameObject>();
        private bool _typesInitialized = false;
        private readonly Helpers.EventSubscriberHelper _eventSubscriber = new Helpers.EventSubscriberHelper();
        
        // 缓存的游戏类型
        private Type? _levelManagerType;
        private Type? _characterCreatorType;
        private Type? _itemAssetsCollectionType;
        private Type? _teamsType;
        private Type? _gameplayDataSettingsType;

        /// <summary>
        /// 获取当前管理的单位数量
        /// </summary>
        public int UnitCount => _managedUnits.Count;

        /// <summary>
        /// 获取所有管理的单位
        /// </summary>
        public IReadOnlyList<GameObject> ManagedUnits => _managedUnits.AsReadOnly();

        public UnitManager()
        {
            InitializeTypes();
            
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
            // 订阅远程角色创建请求
            _eventSubscriber.Subscribe<CreateRemoteCharacterRequestEvent>(OnCreateRemoteCharacterRequested);
            
            // 如果 GameContext 已初始化，立即完成订阅
            _eventSubscriber.EnsureInitializedAndSubscribe();
            
            UnityEngine.Debug.Log("[UnitManager] 已订阅 EventBus 事件");
        }

        /// <summary>
        /// 处理创建远程角色请求
        /// </summary>
        private void OnCreateRemoteCharacterRequested(CreateRemoteCharacterRequestEvent evt)
        {
            if (string.IsNullOrEmpty(evt.PlayerId))
            {
                UnityEngine.Debug.LogWarning("[UnitManager] 远程角色创建请求：PlayerId 为空");
                PublishCharacterCreated(evt.PlayerId, null);
                return;
            }

            try
            {
                UnityEngine.Debug.Log($"[UnitManager] 处理远程角色创建请求: {evt.PlayerId}");

                // 创建远程玩家角色（team=1, 默认属性）
                var character = CreateUnit(
                    $"RemotePlayer_{evt.PlayerId}", 
                    Vector3.zero, 
                    team: 1, 
                    stats: UnitStats.Default
                );

                if (character != null)
                {
                    UnityEngine.Debug.Log($"[UnitManager] ✅ 远程角色创建成功: {evt.PlayerId}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[UnitManager] 远程角色创建失败: {evt.PlayerId}");
                }

                // 发布角色创建完成事件
                PublishCharacterCreated(evt.PlayerId, character);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UnitManager] 处理远程角色创建请求失败: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
                PublishCharacterCreated(evt.PlayerId, null);
            }
        }

        /// <summary>
        /// 发布角色创建完成事件
        /// </summary>
        private void PublishCharacterCreated(string playerId, GameObject? character)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteCharacterCreatedEvent(playerId, character));
            }
        }

        /// <summary>
        /// 初始化游戏类型引用
        /// </summary>
        private void InitializeTypes()
        {
            try
            {
                _levelManagerType = AccessTools.TypeByName("LevelManager");
                _characterCreatorType = AccessTools.TypeByName("CharacterCreator");
                _itemAssetsCollectionType = AccessTools.TypeByName("ItemStatsSystem.ItemAssetsCollection");
                _teamsType = AccessTools.TypeByName("Teams");
                _gameplayDataSettingsType = AccessTools.TypeByName("Duckov.Utilities.GameplayDataSettings");

                _typesInitialized = _levelManagerType != null 
                    && _characterCreatorType != null 
                    && _itemAssetsCollectionType != null
                    && _teamsType != null
                    && _gameplayDataSettingsType != null;

                if (_typesInitialized)
                {
                    UnityEngine.Debug.Log("[UnitManager] 游戏类型初始化成功");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[UnitManager] 部分游戏类型初始化失败");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UnitManager] 类型初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建角色单位
        /// </summary>
        public GameObject? CreateUnit(string name, Vector3 position, int team, UnitStats stats, CharacterCustomization? customization = null)
        {
            object? customData = null;
            if (customization.HasValue && GameContext.IsInitialized)
            {
                var customizationManager = GameContext.Instance.CharacterCustomizationManager;
                customData = customizationManager.CreateCustomization(customization.Value);
            }

            return CreateUnitInternal(name, position, team, stats, customData);
        }

        /// <summary>
        /// 从自定义数据创建角色单位（用于从JSON导入）
        /// </summary>
        public GameObject? CreateUnitFromCustomData(string name, Vector3 position, int team, UnitStats stats, object customData)
        {
            return CreateUnitInternal(name, position, team, stats, customData);
        }

        /// <summary>
        /// 内部创建角色单位的实现
        /// </summary>
        private GameObject? CreateUnitInternal(string name, Vector3 position, int team, UnitStats stats, object? customData)
        {
            if (!_typesInitialized)
            {
                UnityEngine.Debug.LogError("[UnitManager] 游戏类型未初始化");
                return null;
            }

            try
            {
                // 1. 获取 LevelManager 和 CharacterCreator
                var instanceProp = AccessTools.Property(_levelManagerType, "Instance");
                object? levelManager = instanceProp?.GetValue(null);
                if (levelManager == null)
                {
                    UnityEngine.Debug.LogError("[UnitManager] LevelManager 未初始化");
                    return null;
                }

                var creatorProp = AccessTools.Property(_levelManagerType, "CharacterCreator");
                object? characterCreator = creatorProp?.GetValue(levelManager);
                if (characterCreator == null)
                {
                    UnityEngine.Debug.LogError("[UnitManager] CharacterCreator 未找到");
                    return null;
                }

                // 2. 创建角色数据
                var itemAssetsProp = AccessTools.Property(_gameplayDataSettingsType, "ItemAssets");
                object? itemAssets = itemAssetsProp?.GetValue(null);
                var defaultItemTypeProp = AccessTools.Property(itemAssets?.GetType(), "DefaultCharacterItemTypeID");
                int itemTypeID = (int)(defaultItemTypeProp?.GetValue(itemAssets) ?? 0);

                var instantiateMethod = AccessTools.Method(_itemAssetsCollectionType, "InstantiateAsync", new[] { typeof(int) });
                object? instantiateTask = instantiateMethod?.Invoke(null, new object[] { itemTypeID });
                object? awaiter = AccessTools.Method(instantiateTask?.GetType(), "GetAwaiter")?.Invoke(instantiateTask, null);
                var getResult = AccessTools.Method(awaiter?.GetType(), "GetResult");
                object? characterItem = getResult?.Invoke(awaiter, null);

                if (characterItem == null)
                {
                    UnityEngine.Debug.LogError("[UnitManager] 角色数据创建失败");
                    return null;
                }

                // 3. 配置角色属性
                ConfigureStats(characterItem, stats);

                // 4. 获取角色模型
                var characterModelField = AccessTools.Field(_levelManagerType, "characterModel");
                object? modelPrefab = characterModelField?.GetValue(levelManager);
                if (modelPrefab == null)
                {
                    var prefabsProp = AccessTools.Property(_gameplayDataSettingsType, "Prefabs");
                    object? prefabs = prefabsProp?.GetValue(null);
                    if (prefabs != null)
                    {
                        var characterModelProp = AccessTools.Property(prefabs.GetType(), "CharacterModel");
                        modelPrefab = characterModelProp?.GetValue(prefabs);
                    }
                    if (modelPrefab == null)
                    {
                        UnityEngine.Debug.LogError("[UnitManager] 角色模型未找到");
                        return null;
                    }
                }

                // 5. 创建角色实例
                var createMethod = AccessTools.Method(_characterCreatorType, "CreateCharacter");
                object? createTask = createMethod?.Invoke(characterCreator, new object[] { 
                    characterItem, modelPrefab, position, Quaternion.identity 
                });
                awaiter = AccessTools.Method(createTask?.GetType(), "GetAwaiter")?.Invoke(createTask, null);
                getResult = AccessTools.Method(awaiter?.GetType(), "GetResult");
                object? newCharacter = getResult?.Invoke(awaiter, null);

                if (newCharacter == null)
                {
                    UnityEngine.Debug.LogError("[UnitManager] 角色实例化失败");
                    return null;
                }

                // 6. 配置角色
                Component? characterComponent = newCharacter as Component;
                if (characterComponent == null)
                {
                    UnityEngine.Debug.LogError("[UnitManager] 角色不是Component类型");
                    return null;
                }

                GameObject unitObject = characterComponent.gameObject;
                unitObject.name = name;
                characterComponent.transform.position = position;

                // 设置队伍
                string[] teamEnumNames = { "player", "scav", "middle" };
                object teamValue = Enum.Parse(_teamsType, teamEnumNames[team]);
                var setTeamMethod = AccessTools.Method(newCharacter.GetType(), "SetTeam");
                setTeamMethod?.Invoke(newCharacter, new object[] { teamValue });

                // 初始化血量
                var healthProp = AccessTools.Property(newCharacter.GetType(), "Health");
                object? health = healthProp?.GetValue(newCharacter);
                var initMethod = AccessTools.Method(health?.GetType(), "Init", Type.EmptyTypes);
                initMethod?.Invoke(health, null);

                // TODO: AI 功能暂时禁用，专注于基础动画测试
                // 后续如果需要 NPC 可以重新启用
                /*
                // 添加AI（非玩家队伍）
                if (team != 0)
                {
                    AddAIController(newCharacter, position);
                    
                    // 设置巡逻中心
                    var aiController = characterComponent.GetComponent(AccessTools.TypeByName("AICharacterController"));
                    if (aiController != null)
                    {
                        var patrolPosField = AccessTools.Field(aiController.GetType(), "patrolPosition");
                        patrolPosField?.SetValue(aiController, position);
                    }
                }
                */

                // 应用自定义外观
                if (customData != null && GameContext.IsInitialized)
                {
                    var customizationManager = GameContext.Instance.CharacterCustomizationManager;
                    customizationManager.ApplyToCharacter(newCharacter, customData);
                }

                // 添加到管理列表
                _managedUnits.Add(unitObject);

                return unitObject;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UnitManager] 创建单位失败: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
                return null;
            }
        }

        /// <summary>
        /// 配置角色属性
        /// </summary>
        private void ConfigureStats(object characterItem, UnitStats stats)
        {
            SetStat(characterItem, "MaxHealth", stats.MaxHealth);
            SetStat(characterItem, "WalkSpeed", stats.WalkSpeed);
            SetStat(characterItem, "RunSpeed", stats.RunSpeed);
            SetStat(characterItem, "PhysicAttack", stats.PhysicAttack);
        }

        /// <summary>
        /// 设置角色属性值
        /// </summary>
        private void SetStat(object characterItem, string statName, float value)
        {
            try
            {
                var getStatMethod = AccessTools.Method(characterItem.GetType(), "GetStat", new[] { typeof(int) });
                int statHash = statName.GetHashCode();
                object? stat = getStatMethod?.Invoke(characterItem, new object[] { statHash });
                if (stat != null)
                {
                    var baseValueProp = AccessTools.Property(stat.GetType(), "BaseValue");
                    baseValueProp?.SetValue(stat, value);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[UnitManager] 设置属性 {statName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 为角色添加AI控制器（暂时禁用）
        /// </summary>
        [Obsolete("AI 功能暂时禁用，专注于基础动画测试")]
        private void AddAIController(object character, Vector3 position)
        {
            try
            {
                Component? characterComponent = character as Component;
                if (characterComponent == null) return;

                GameObject characterGO = characterComponent.gameObject;
                
                Type? seekerType = AccessTools.TypeByName("Pathfinding.Seeker");
                Type? pathControlType = AccessTools.TypeByName("AI_PathControl");
                Type? aiControllerType = AccessTools.TypeByName("AICharacterController");
                
                if (seekerType == null || pathControlType == null || aiControllerType == null)
                {
                    UnityEngine.Debug.LogWarning("[UnitManager] AI类型未找到");
                    return;
                }

                // 添加组件
                object? seeker = characterGO.AddComponent(seekerType);
                object? pathControl = characterGO.AddComponent(pathControlType);
                object? aiController = characterGO.AddComponent(aiControllerType);

                if (seeker == null || pathControl == null || aiController == null)
                {
                    UnityEngine.Debug.LogWarning("[UnitManager] AI组件添加失败");
                    return;
                }

                // 连接引用
                var pathControlSeekerField = AccessTools.Field(pathControlType, "seeker");
                var pathControlControllerField = AccessTools.Field(pathControlType, "controller");
                pathControlSeekerField?.SetValue(pathControl, seeker);
                pathControlControllerField?.SetValue(pathControl, character);

                var aiPathControlField = AccessTools.Field(aiControllerType, "pathControl");
                aiPathControlField?.SetValue(aiController, pathControl);

                // 配置AI参数
                SetAIField(aiController, "sightDistance", 15f);
                SetAIField(aiController, "sightAngle", 100f);
                SetAIField(aiController, "baseReactionTime", 0.3f);
                SetAIField(aiController, "reactionTime", 0.3f);
                SetAIField(aiController, "patrolRange", 8f);
                SetAIField(aiController, "defaultWeaponOut", true);

                // 初始化AI
                Type? voiceType = AccessTools.TypeByName("Duckov.AudioManager+VoiceType") 
                    ?? AccessTools.TypeByName("AudioManager+VoiceType");
                Type? footstepType = AccessTools.TypeByName("Duckov.AudioManager+FootStepMaterialType")
                    ?? AccessTools.TypeByName("AudioManager+FootStepMaterialType");
                
                if (voiceType != null && footstepType != null)
                {
                    var initMethod = AccessTools.Method(aiControllerType, "Init",
                        new[] { character.GetType(), typeof(Vector3), voiceType, footstepType });
                    
                    if (initMethod != null)
                    {
                        object defaultVoice = Enum.ToObject(voiceType, 0);
                        object defaultFootstep = Enum.ToObject(footstepType, 0);
                        initMethod.Invoke(aiController, new object[] { character, position, defaultVoice, defaultFootstep });
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[UnitManager] 添加AI失败: {ex.Message}");
            }
        }

        private void SetAIField(object aiController, string fieldName, object value)
        {
            try
            {
                var field = AccessTools.Field(aiController.GetType(), fieldName);
                field?.SetValue(aiController, value);
            }
            catch { }
        }

        /// <summary>
        /// 销毁单位
        /// </summary>
        public bool DestroyUnit(GameObject unit)
        {
            if (unit == null) return false;

            if (_managedUnits.Remove(unit))
            {
                UnityEngine.Object.Destroy(unit);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 销毁所有管理的单位
        /// </summary>
        public void DestroyAllUnits()
        {
            foreach (var unit in _managedUnits)
            {
                if (unit != null)
                {
                    UnityEngine.Object.Destroy(unit);
                }
            }
            _managedUnits.Clear();
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
            DestroyAllUnits();
        }
    }

    /// <summary>
    /// 单位属性配置
    /// </summary>
    public struct UnitStats
    {
        public float MaxHealth;
        public float WalkSpeed;
        public float RunSpeed;
        public float PhysicAttack;

        public static UnitStats Default => new UnitStats
        {
            MaxHealth = 100f,
            WalkSpeed = 2f,
            RunSpeed = 5f,
            PhysicAttack = 10f
        };

        public static UnitStats Fast => new UnitStats
        {
            MaxHealth = 80f,
            WalkSpeed = 3f,
            RunSpeed = 7f,
            PhysicAttack = 8f
        };

        public static UnitStats Tank => new UnitStats
        {
            MaxHealth = 200f,
            WalkSpeed = 1.5f,
            RunSpeed = 3f,
            PhysicAttack = 15f
        };
    }
}


