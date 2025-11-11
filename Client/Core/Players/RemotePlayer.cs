using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.Utils;
using DuckyNet.Client.Core.EventBus;
using DuckyNet.Client.Core.EventBus.Events;
using ItemStatsSystem;
using Duckov.Utilities;
using CharacterAppearanceReceivedEvent = DuckyNet.Client.Services.CharacterAppearanceReceivedEvent;

namespace DuckyNet.Client.Core.Players
{
    /// <summary>
    /// 远程玩家 - 表示网络中的其他玩家
    /// 🔥 正确架构：双层生命周期
    /// 
    /// RemotePlayer 生命周期（房间层）：
    /// - PlayerJoinedRoomEvent → 创建 RemotePlayer（订阅位置同步事件）
    /// - PlayerLeftRoomEvent → 销毁 RemotePlayer
    /// 
    /// Character 生命周期（场景层）：
    /// - PlayerEnteredSceneEvent → 标记玩家进入场景
    /// - 收到位置同步数据 → 创建角色（如果在同一场景）
    /// - PlayerLeftSceneEvent → 销毁角色（保留 RemotePlayer）
    /// 
    /// 性能优化：
    /// - 缓存 Transform 引用，减少 GetComponent 调用
    /// </summary>
    public class RemotePlayer : BasePlayer
    {
        #region 常量定义

        /// <summary>等待角色初始化的帧数</summary>
        private const int CHARACTER_INIT_WAIT_FRAMES = 2;

        /// <summary>默认生成位置</summary>
        private static readonly Vector3 DEFAULT_SPAWN_POSITION = Vector3.zero;

        #endregion

        #region 缓存字段

        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
        private SmoothSyncManager? _smoothSyncManager;
        private Transform? _characterTransform; // 缓存 Transform 引用
        private CharacterAppearanceData? _cachedAppearanceData; // 缓存外观数据
        private PlayerEquipmentData? _equipmentData; // 缓存装备数据
        private PlayerWeaponData? _weaponData; // 缓存武器数据
        
        // 血量同步缓存
        private object? _cachedHealth; // 缓存 Health 组件
        private System.Reflection.MethodInfo? _cachedSetHealthMethod; // 缓存 SetHealth 方法

        /// <summary>装备槽位Hash映射缓存</summary>
        private static readonly Dictionary<EquipmentSlotType, int> _equipmentSlotHashCache = new Dictionary<EquipmentSlotType, int>()
        {
            { EquipmentSlotType.Armor, CharacterEquipmentController.armorHash },
            { EquipmentSlotType.Helmet, CharacterEquipmentController.helmatHash },
            { EquipmentSlotType.FaceMask, CharacterEquipmentController.faceMaskHash },
            { EquipmentSlotType.Backpack, CharacterEquipmentController.backpackHash },
            { EquipmentSlotType.Headset, CharacterEquipmentController.headsetHash }
        };

        /// <summary>武器槽位Hash映射缓存</summary>
        private static readonly Dictionary<WeaponSlotType, int> _weaponSlotHashCache = new Dictionary<WeaponSlotType, int>()
        {
            { WeaponSlotType.PrimaryWeapon, "PrimaryWeapon".GetHashCode() },
            { WeaponSlotType.SecondaryWeapon, "SecondaryWeapon".GetHashCode() },
            { WeaponSlotType.MeleeWeapon, "MeleeWeapon".GetHashCode() }
        };

        #endregion

        /// <summary>
        /// 远程玩家当前所在的场景名称
        /// </summary>
        public string? CurrentSceneName { get; private set; }

        public RemotePlayer(PlayerInfo info) : base(info)
        {
            Log($"[RemotePlayer] 远程玩家创建（房间层）: {info.SteamName} ({info.SteamId})");

            InitializeSceneName(info);
            SubscribeToEvents();
            RequestAppearanceData();

            Log($"[RemotePlayer] 🎨 远程玩家创建完成: {info.SteamName}");
        }

        /// <summary>
        /// 初始化场景名称
        /// </summary>
        private void InitializeSceneName(PlayerInfo info)
        {
            if (info.CurrentScenelData != null && !string.IsNullOrEmpty(info.CurrentScenelData.SceneName))
            {
                CurrentSceneName = info.CurrentScenelData.SceneName;
                Log($"[RemotePlayer] 初始场景: {CurrentSceneName}");
            }
            else
            {
                Log($"[RemotePlayer] 玩家 {info.SteamName} 初始场景未设置");
            }
        }

        /// <summary>
        /// 订阅所有事件
        /// </summary>
        private void SubscribeToEvents()
        {
            _eventSubscriber.EnsureInitializedAndSubscribe();

            // 订阅位置同步事件
            _eventSubscriber.Subscribe<PlayerUnitySyncEvent>(OnPlayerUnitySyncReceived);

            // 订阅场景事件（远程玩家进入/离开场景）
            _eventSubscriber.Subscribe<PlayerEnteredSceneEvent>(OnPlayerEnteredScene);
            _eventSubscriber.Subscribe<PlayerLeftSceneEvent>(OnPlayerLeftScene);

            // 订阅外观接收事件
            _eventSubscriber.Subscribe<CharacterAppearanceReceivedEvent>(OnAppearanceReceived);
            _eventSubscriber.Subscribe<BeforeDamageAppliedEvent>(OnBeforeDamageApplied);

            // 订阅血量同步事件
            _eventSubscriber.Subscribe<RemotePlayerHealthSyncEvent>(OnHealthSyncReceived);

        }


        /// <summary>
        /// 处理血量同步事件
        /// </summary>
        private void OnHealthSyncReceived(RemotePlayerHealthSyncEvent @event)
        {
            try
            {
                // 只处理自己的血量同步数据
                if (@event.HealthData.SteamId != Info.SteamId)
                {
                    return;
                }

                // 检查角色是否已创建
                if (CharacterObject == null)
                {
                    Log($"[RemotePlayer] ⚠️ 角色尚未创建，无法同步血量");
                    return;
                }

                // 如果缓存失效，重新获取 Health 组件
                if (_cachedHealth == null || _cachedSetHealthMethod == null)
                {
                    Log($"[RemotePlayer] 🔧 Health 缓存未初始化，正在初始化...");
                    if (!InitializeHealthCache())
                    {
                        LogError($"[RemotePlayer] ❌ Health 缓存初始化失败，跳过血量同步");
                        return;
                    }
                }

                // 读取当前血量（调用前）
                var healthType = _cachedHealth!.GetType();
                var currentHealthProp = HarmonyLib.AccessTools.Property(healthType, "CurrentHealth");
                float beforeHealth = currentHealthProp != null ? (float)(currentHealthProp.GetValue(_cachedHealth) ?? 0f) : 0f;

                // 使用缓存的 SetHealth 方法设置当前血量
                _cachedSetHealthMethod!.Invoke(_cachedHealth, new object[] { @event.HealthData.CurrentHealth });

                // 读取当前血量（调用后，验证是否设置成功）
                float afterHealth = currentHealthProp != null ? (float)(currentHealthProp.GetValue(_cachedHealth) ?? 0f) : 0f;

                Log($"[RemotePlayer] 💚 同步血量: {beforeHealth:F0} → {afterHealth:F0} (目标:{@event.HealthData.CurrentHealth:F0}/{@event.HealthData.MaxHealth:F0})");

                // 验证是否设置成功
                if (Math.Abs(afterHealth - @event.HealthData.CurrentHealth) > 0.1f)
                {
                    LogWarning($"[RemotePlayer] ⚠️ 血量设置不准确！期望:{@event.HealthData.CurrentHealth:F0}, 实际:{afterHealth:F0}");
                }

                // 🔥 手动触发 HealthBar 刷新（确保 UI 更新）
                RefreshHealthBar();
            }
            catch (Exception ex)
            {
                // 缓存可能失效，清空缓存
                _cachedHealth = null;
                _cachedSetHealthMethod = null;
                LogError($"[RemotePlayer] 处理血量同步失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 初始化 Health 缓存
        /// </summary>
        private bool InitializeHealthCache()
        {
            try
            {
                // 获取 CharacterMainControl 组件
                var characterMainControlType = HarmonyLib.AccessTools.TypeByName("CharacterMainControl");
                if (characterMainControlType == null)
                {
                    LogError("[RemotePlayer] 找不到 CharacterMainControl 类型");
                    return false;
                }

                var characterMainControl = CharacterObject!.GetComponent(characterMainControlType);
                if (characterMainControl == null)
                {
                    LogError("[RemotePlayer] CharacterObject 上没有 CharacterMainControl 组件");
                    return false;
                }

                // 获取 Health 属性
                var healthProperty = HarmonyLib.AccessTools.Property(characterMainControlType, "Health");
                if (healthProperty == null)
                {
                    LogError("[RemotePlayer] 找不到 Health 属性");
                    return false;
                }

                _cachedHealth = healthProperty.GetValue(characterMainControl);
                if (_cachedHealth == null)
                {
                    LogError("[RemotePlayer] Health 组件为空");
                    return false;
                }

                // 缓存 SetHealth 方法
                var healthType = _cachedHealth.GetType();
                _cachedSetHealthMethod = HarmonyLib.AccessTools.Method(healthType, "SetHealth");
                if (_cachedSetHealthMethod == null)
                {
                    LogError("[RemotePlayer] 找不到 SetHealth 方法");
                    _cachedHealth = null;
                    return false;
                }

                // 🔥 关键修复：调用 SetItemAndCharacter 绑定 item
                // Health.MaxHealth 需要从 item.GetStatValue() 读取，如果 item 为 null，MaxHealth 就是 0
                Log($"[RemotePlayer] 🔍 正在获取 CharacterItem...");
                var characterItemProp = HarmonyLib.AccessTools.Property(characterMainControlType, "CharacterItem");
                if (characterItemProp == null)
                {
                    LogError("[RemotePlayer] 找不到 CharacterItem 属性");
                }
                else
                {
                    var characterItem = characterItemProp.GetValue(characterMainControl);
                    Log($"[RemotePlayer] CharacterItem: {(characterItem != null ? characterItem.GetType().Name : "null")}");
                    
                    if (characterItem != null)
                    {
                        Log($"[RemotePlayer] 🔍 正在查找 SetItemAndCharacter 方法...");
                        var setItemAndCharacterMethod = HarmonyLib.AccessTools.Method(healthType, "SetItemAndCharacter");
                        if (setItemAndCharacterMethod != null)
                        {
                            Log($"[RemotePlayer] 🔧 正在调用 Health.SetItemAndCharacter()...");
                            setItemAndCharacterMethod.Invoke(_cachedHealth, new object[] { characterItem, characterMainControl });
                            Log($"[RemotePlayer] ✅ 已调用 Health.SetItemAndCharacter()");
                            
                            // 验证 item 字段是否设置成功
                            var itemField = HarmonyLib.AccessTools.Field(healthType, "item");
                            var itemValue = itemField?.GetValue(_cachedHealth);
                            Log($"[RemotePlayer] 验证 Health.item: {(itemValue != null ? "已设置" : "null")}");
                        }
                        else
                        {
                            LogError("[RemotePlayer] ❌ 找不到 SetItemAndCharacter 方法");
                        }
                    }
                    else
                    {
                        LogError("[RemotePlayer] ❌ CharacterItem 为 null，无法绑定到 Health");
                    }
                }

                // 🔥 确保 showHealthBar = true
                var showHealthBarProp = HarmonyLib.AccessTools.Property(healthType, "showHealthBar");
                if (showHealthBarProp != null && showHealthBarProp.CanWrite)
                {
                    showHealthBarProp.SetValue(_cachedHealth, true);
                }

                // 验证 MaxHealth 是否正确
                var maxHealthProp = HarmonyLib.AccessTools.Property(healthType, "MaxHealth");
                float maxHealth = maxHealthProp != null ? (float)(maxHealthProp.GetValue(_cachedHealth) ?? 0f) : 0f;
                Log($"[RemotePlayer] ✅ Health 缓存初始化成功，MaxHealth={maxHealth:F0}");
                
                return true;
            }
            catch (Exception ex)
            {
                LogError($"[RemotePlayer] 初始化 Health 缓存失败: {ex.Message}");
                _cachedHealth = null;
                _cachedSetHealthMethod = null;
                return false;
            }
        }


        /// <summary>
        /// 刷新血条显示（同时刷新名字）
        /// </summary>
        private void RefreshHealthBar()
        {
            try
            {
                if (_cachedHealth == null) return;

                // 调用 RequestHealthBar 方法强制刷新
                var requestHealthBarMethod = HarmonyLib.AccessTools.Method(_cachedHealth.GetType(), "RequestHealthBar");
                if (requestHealthBarMethod != null)
                {
                    requestHealthBarMethod.Invoke(_cachedHealth, null);
                }

                // 🔥 血量同步时也刷新名字（防止被 RefreshCharacterIcon 覆盖）
                RefreshHealthBarName();

                Log($"[RemotePlayer] 🔄 已触发 HealthBar 刷新");
            }
            catch (Exception ex)
            {
                LogWarning($"[RemotePlayer] 刷新 HealthBar 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新血条名字显示
        /// </summary>
        private void RefreshHealthBarName()
        {
            try
            {
                if (_cachedHealth == null) return;

                // 获取 HealthBarManager
                var healthBarManagerType = HarmonyLib.AccessTools.TypeByName("Duckov.UI.HealthBarManager");
                if (healthBarManagerType == null) return;

                var instanceProp = HarmonyLib.AccessTools.Property(healthBarManagerType, "Instance");
                var healthBarManager = instanceProp?.GetValue(null);
                if (healthBarManager == null) return;

                // 获取当前 Health 对应的 HealthBar
                var getActiveHealthBarMethod = HarmonyLib.AccessTools.Method(healthBarManagerType, "GetActiveHealthBar");
                if (getActiveHealthBarMethod == null) return;

                var healthBar = getActiveHealthBarMethod.Invoke(healthBarManager, new object[] { _cachedHealth });
                if (healthBar == null) return;

                // 强制刷新图标（会重新读取 characterPreset.showName）
                var refreshIconMethod = HarmonyLib.AccessTools.Method(healthBar.GetType(), "RefreshCharacterIcon");
                if (refreshIconMethod != null)
                {
                    refreshIconMethod.Invoke(healthBar, null);
                }

                // 直接设置名字文本（双重保险）
                var nameTextField = HarmonyLib.AccessTools.Field(healthBar.GetType(), "nameText");
                var nameText = nameTextField?.GetValue(healthBar);
                
                if (nameText != null)
                {
                    var textProp = HarmonyLib.AccessTools.Property(nameText.GetType(), "text");
                    if (textProp != null && textProp.CanWrite)
                    {
                        textProp.SetValue(nameText, Info.SteamName);
                    }
                    
                    // 强制激活名字显示
                    var gameObjectProp = HarmonyLib.AccessTools.Property(nameText.GetType(), "gameObject");
                    var gameObject = gameObjectProp?.GetValue(nameText);
                    if (gameObject != null)
                    {
                        var setActiveMethod = HarmonyLib.AccessTools.Method(gameObject.GetType(), "SetActive");
                        setActiveMethod?.Invoke(gameObject, new object[] { true });
                    }
                }
            }
            catch (Exception ex)
            {
                // 静默失败，不影响血量同步
                LogWarning($"[RemotePlayer] 刷新血条名字失败: {ex.Message}");
            }
        }

        private void OnBeforeDamageApplied(BeforeDamageAppliedEvent @event)
        {
            // 判断受伤的是否是当前 LocalPlayer 实例的角色
            if (@event.TargetGameObject == null || CharacterObject == null)
            {
                return;
            }

            // 通过 GameObject 引用判断是否是当前单位
            if (@event.TargetGameObject != CharacterObject)
            {
                return;
            }

            // 将当前单位所有伤害设置为 0（无敌模式）
            @event.DamageValue = 0;
        }

        #region 场景事件处理

        /// <summary>
        /// 远程玩家进入场景 - 销毁旧角色并创建新角色
        /// </summary>
        private void OnPlayerEnteredScene(PlayerEnteredSceneEvent @event)
        {
            Log($"[RemotePlayer] ========== PlayerEnteredSceneEvent 接收 ==========");
            Log($"[RemotePlayer] 事件玩家: {@event.PlayerInfo.SteamName} ({@event.PlayerInfo.SteamId})");
            Log($"[RemotePlayer] 当前对象: {Info.SteamName} ({Info.SteamId})");
            Log($"[RemotePlayer] 是否匹配: {@event.PlayerInfo.SteamId == Info.SteamId}");
            
            // 只处理自己的场景事件
            if (@event.PlayerInfo.SteamId != Info.SteamId)
            {
                Log($"[RemotePlayer] ⚠️ SteamId 不匹配，跳过");
                return;
            }

            // 先销毁旧角色
            if (CharacterObject != null)
            {
                Log($"[RemotePlayer] 销毁旧角色对象");
                UnityEngine.Object.Destroy(CharacterObject);
                CharacterObject = null;
                _characterTransform = null;
            }

            // 更新场景信息
            CurrentSceneName = @event.ScenelData.SceneName;
            Info.CurrentScenelData = @event.ScenelData;

            Log($"[RemotePlayer] 🎯 玩家 {Info.SteamName} 进入场景: {CurrentSceneName}");
            Log($"[RemotePlayer] 🎯 正在创建角色对象...");

            // 创建新角色
            CreateCharacter(DEFAULT_SPAWN_POSITION, Info.SteamName);
            
            Log($"[RemotePlayer] ========== 角色创建完成 ==========");
        }

        /// <summary>
        /// 玩家离开场景 - 销毁角色
        /// </summary>
        private void OnPlayerLeftScene(PlayerLeftSceneEvent @event)
        {
            // 只处理自己的场景事件
            if (@event.PlayerInfo.SteamId != Info.SteamId) return;

            Log($"[RemotePlayer] 玩家 {Info.SteamName} 离开场景: {CurrentSceneName}");

            // 清空场景信息
            CurrentSceneName = null;
            Info.CurrentScenelData = new ScenelData("", "");

            // 销毁角色，但保留 RemotePlayer
            DestroyCharacter();
        }

        #endregion

        #region 位置同步

        /// <summary>
        /// 接收位置同步数据
        /// </summary>
        private void OnPlayerUnitySyncReceived(PlayerUnitySyncEvent @event)
        {
            // 快速过滤：检查同步数据是否是当前玩家的
            if (@event.SteamID != Info.SteamId) return;

            // 如果平滑管理器不存在，初始化它
            if (_smoothSyncManager == null)
            {
                InitializeSmoothSyncManager(@event.SyncData);
            }

            // 接收新的同步数据
            _smoothSyncManager?.ReceiveSyncData(@event.SyncData);
        }

        /// <summary>
        /// 初始化平滑同步管理器
        /// </summary>
        private void InitializeSmoothSyncManager(UnitySyncData syncData)
        {
            var position = syncData.GetPosition();
            var rotation = syncData.GetRotation();

            _smoothSyncManager = new SmoothSyncManager(
                new Vector3(position.Item1, position.Item2, position.Item3),
                new Quaternion(rotation.Item1, rotation.Item2, rotation.Item3, rotation.Item4)
            );

            Log($"[RemotePlayer] 初始化平滑同步管理器: {Info.SteamName}");
        }

        /// <summary>
        /// 更新远程玩家位置（每帧调用）
        /// 性能优化：缓存 Transform 引用，避免每帧 GetComponent
        /// </summary>
        public void UpdatePosition()
        {
            if (_smoothSyncManager == null || CharacterObject == null) return;

            // 缓存 Transform 引用
            if (_characterTransform == null)
            {
                _characterTransform = CharacterObject.transform;
                if (_characterTransform == null) return;
            }

            // 更新平滑值并应用到角色对象
            _smoothSyncManager.Update();
            _smoothSyncManager.ApplyToTransform(_characterTransform, _characterTransform);
            
            // 🔥 每帧强制激活名字显示（防止被 RefreshCharacterIcon 隐藏）
            ForceShowHealthBarName();
        }
        
        private int _nameRefreshFrameCounter = 0;
        
        /// <summary>
        /// 强制激活血条名字显示（每帧调用，但限制频率）
        /// </summary>
        private void ForceShowHealthBarName()
        {
            // 每 30 帧刷新一次（约 0.5 秒）避免性能问题
            _nameRefreshFrameCounter++;
            if (_nameRefreshFrameCounter < 30) return;
            _nameRefreshFrameCounter = 0;
            
            try
            {
                if (_cachedHealth == null) return;

                // 获取 HealthBarManager
                var healthBarManagerType = HarmonyLib.AccessTools.TypeByName("Duckov.UI.HealthBarManager");
                if (healthBarManagerType == null) return;

                var instanceProp = HarmonyLib.AccessTools.Property(healthBarManagerType, "Instance");
                var healthBarManager = instanceProp?.GetValue(null);
                if (healthBarManager == null) return;

                // 获取当前 Health 对应的 HealthBar
                var getActiveHealthBarMethod = HarmonyLib.AccessTools.Method(healthBarManagerType, "GetActiveHealthBar");
                if (getActiveHealthBarMethod == null) return;

                var healthBar = getActiveHealthBarMethod.Invoke(healthBarManager, new object[] { _cachedHealth });
                if (healthBar == null) return;

                // 直接激活名字显示（不调用 RefreshCharacterIcon，避免被覆盖）
                var nameTextField = HarmonyLib.AccessTools.Field(healthBar.GetType(), "nameText");
                var nameText = nameTextField?.GetValue(healthBar);
                
                if (nameText != null)
                {
                    // 强制激活名字的 GameObject
                    var gameObjectProp = HarmonyLib.AccessTools.Property(nameText.GetType(), "gameObject");
                    var gameObject = gameObjectProp?.GetValue(nameText);
                    if (gameObject != null)
                    {
                        var setActiveMethod = HarmonyLib.AccessTools.Method(gameObject.GetType(), "SetActive");
                        setActiveMethod?.Invoke(gameObject, new object[] { true });
                    }
                    
                    // 确保文本正确
                    var textProp = HarmonyLib.AccessTools.Property(nameText.GetType(), "text");
                    if (textProp != null && textProp.CanWrite)
                    {
                        string currentText = textProp.GetValue(nameText)?.ToString() ?? "";
                        if (currentText != Info.SteamName)
                        {
                            textProp.SetValue(nameText, Info.SteamName);
                        }
                    }
                }
            }
            catch
            {
                // 静默失败，不影响位置同步
            }
        }

        #endregion

        #region 角色创建

        /// <summary>
        /// 创建角色对象（主入口）
        /// </summary>
        /// <param name="position">生成位置</param>
        /// <param name="displayName">显示名称（可选，默认使用 Info.SteamName）</param>
        /// <returns>创建成功返回true</returns>
        public bool CreateCharacter(Vector3 position, string? displayName = null)
        {
            displayName ??= Info.SteamName;

            // 如果已经有角色对象,先销毁
            if (CharacterObject != null)
            {
                DestroyCharacter();
            }

            try
            {
                // 1. 创建角色模型
                var newCharacter = CreateCharacterModel(position, displayName);
                if (newCharacter == null)
                {
                    return false;
                }

                // 2. 配置角色组件
                if (!ConfigureCharacterComponents(newCharacter, displayName))
                {
                    return false;
                }

                // 3. 初始化系统
                InitializeCharacterSystems();

                // 4. 应用数据（外观、装备、武器）
                ApplyCharacterDataDelayed(displayName);

                // 5. 发布事件和日志
                PublishCharacterCreatedEvent();
                LogCharacterCreationSuccess(displayName, position);

                return true;
            }
            catch (Exception ex)
            {
                LogError($"[RemotePlayer] ❌ 创建角色时发生异常: {displayName}, 错误: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 创建角色模型
        /// </summary>
        private object? CreateCharacterModel(Vector3 position, string displayName)
        {
            // 创建角色数据项
            var characterItem = CharacterCreationUtils.CreateCharacterItem();
            if (characterItem == null)
            {
                LogWarning($"[RemotePlayer] ⚠️ 创建角色数据项失败: {displayName}");
                return null;
            }

            // 获取角色模型预制体
            var modelPrefab = CharacterCreationUtils.GetCharacterModelPrefab();
            if (modelPrefab == null)
            {
                LogWarning($"[RemotePlayer] ⚠️ 获取角色模型预制体失败（可能是场景切换中 LevelManager 未就绪）: {displayName}");
                return null;
            }

            // 实例化角色
            var newCharacter = CharacterCreationUtils.CreateCharacterInstance(
                characterItem, modelPrefab, position, Quaternion.identity
            );

            if (newCharacter == null)
            {
                LogWarning($"[RemotePlayer] ⚠️ 实例化角色失败: {displayName}");
            }

            return newCharacter;
        }

        /// <summary>
        /// 配置角色组件和属性
        /// </summary>
        private bool ConfigureCharacterComponents(object newCharacter, string displayName)
        {
            // 配置角色基本属性
            CharacterCreationUtils.ConfigureCharacter(newCharacter, $"Character_{Info.SteamName}", DEFAULT_SPAWN_POSITION, team: 0);
            CharacterCreationUtils.ConfigureCharacterPreset(newCharacter, displayName, showName: true);

            // 标记为远程玩家
            CharacterCreationUtils.MarkAsRemotePlayer(newCharacter);

            // 从距离管理系统中移除
            CharacterCreationUtils.UnregisterFromDistanceSystem(newCharacter);

            // 请求血条
            var customIcon = GetCustomIcon();
            CharacterCreationUtils.RequestHealthBar(newCharacter, displayName, customIcon);

            // 保存 GameObject 引用
            if (newCharacter is Component characterComponent)
            {
                CharacterObject = characterComponent.gameObject;
                _characterTransform = CharacterObject.transform;

                // 确保 GameObject 激活
                if (!CharacterObject.activeSelf)
                {
                    LogWarning($"[RemotePlayer] ⚠️ GameObject 未激活，强制激活");
                    CharacterObject.SetActive(true);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 初始化角色系统（同步管理器等）
        /// </summary>
        private void InitializeCharacterSystems()
        {
            if (_characterTransform == null) return;

            // 初始化平滑同步管理器（如果还没有）
            if (_smoothSyncManager == null)
            {
                _smoothSyncManager = new SmoothSyncManager(
                    _characterTransform.position,
                    _characterTransform.rotation
                );
                Log($"[RemotePlayer] 创建平滑同步管理器: 位置 {_characterTransform.position}");
            }
        }

        /// <summary>
        /// 延迟应用角色数据（外观、装备、武器）
        /// </summary>
        private void ApplyCharacterDataDelayed(string displayName)
        {
            if (ModBehaviour.Instance != null)
            {
                // 使用协程延迟应用
                ModBehaviour.Instance.StartCoroutine(ApplyAllDataDelayed());
            }
            else
            {
                // 直接应用（可能失败）
                LogWarning($"[RemotePlayer] ⚠️ ModBehaviour 不可用，立即应用数据（可能失败）");
                ApplyCachedAppearance();
                ApplyCachedEquipment();
                ApplyCachedWeapons();
            }
        }

        /// <summary>
        /// 发布角色创建事件
        /// </summary>
        private void PublishCharacterCreatedEvent()
        {
            if (GameContext.IsInitialized && GameContext.Instance.EventBus != null && CharacterObject != null)
            {
                GameContext.Instance.EventBus.Publish(
                    new RemoteCharacterCreatedEvent(Info.SteamId, CharacterObject)
                );
            }
        }

        /// <summary>
        /// 记录角色创建成功日志
        /// </summary>
        private void LogCharacterCreationSuccess(string displayName, Vector3 position)
        {
            if (_characterTransform == null || CharacterObject == null) return;

            Log($"[RemotePlayer] ✅ 角色创建成功: {displayName}, 位置: {_characterTransform.position}");

            // 验证场景
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Log($"[RemotePlayer] 场景: {CharacterObject.scene.name} (活动: {activeScene.name})");
        }

        /// <summary>
        /// 获取自定义图标 - 使用 Steam 头像
        /// </summary>
        private UnityEngine.Sprite? GetCustomIcon()
        {
            // 如果有 Steam 头像,将其转换为 Sprite
            if (AvatarTexture != null)
            {
                return UnityEngine.Sprite.Create(
                    AvatarTexture,
                    new UnityEngine.Rect(0, 0, AvatarTexture.width, AvatarTexture.height),
                    new UnityEngine.Vector2(0.5f, 0.5f)
                );
            }
            return null;
        }

        /// <summary>
        /// 请求该玩家的外观数据
        /// </summary>
        private void RequestAppearanceData()
        {
            if (GameContext.IsInitialized && GameContext.Instance.RpcClient != null)
            {
                Log($"[RemotePlayer] 📤 正在请求玩家外观数据: {Info.SteamName} ({Info.SteamId})");
                GameContext.Instance.RpcClient.InvokeServer<Shared.Services.ICharacterAppearanceService>(
                    nameof(Shared.Services.ICharacterAppearanceService.RequestAppearance),
                    Info.SteamId
                );
                Log($"[RemotePlayer] ✅ 外观数据请求已发送");
            }
            else
            {
                LogWarning($"[RemotePlayer] ❌ RpcClient未初始化，无法请求外观数据: {Info.SteamName}");
            }
        }

        /// <summary>
        /// 接收到外观数据事件
        /// </summary>
        private void OnAppearanceReceived(Services.CharacterAppearanceReceivedEvent @event)
        {
            // 只处理自己的外观数据
            if (@event.SteamId != Info.SteamId)
            {
                Log($"[RemotePlayer] 🔍 收到其他玩家的外观数据，忽略: {@event.SteamId} (当前: {Info.SteamId})");
                return;
            }

            Log($"[RemotePlayer] 📦 收到玩家外观数据: {Info.SteamName} ({Info.SteamId})");
            Log($"[RemotePlayer] 外观数据详情 - HeadScale: {@event.AppearanceData.HeadSetting.ScaleX}, Parts: {@event.AppearanceData.Parts.Length}");

            // 缓存外观数据
            _cachedAppearanceData = @event.AppearanceData;

            // 如果角色已创建,立即应用外观
            if (CharacterObject != null)
            {
                Log($"[RemotePlayer] ✅ 角色对象已存在，立即应用外观: {Info.SteamName}");
                ApplyCachedAppearance();
            }
            else
            {
                Log($"[RemotePlayer] 💾 角色对象尚未创建，外观数据已缓存，将在角色创建后应用: {Info.SteamName}");
            }
        }

        /// <summary>
        /// 应用缓存的外观数据
        /// </summary>
        private void ApplyCachedAppearance()
        {
            if (_cachedAppearanceData == null)
            {
                LogWarning($"[RemotePlayer] ⚠️ 没有缓存的外观数据: {Info.SteamName}");
                return;
            }

            if (CharacterObject == null)
            {
                LogWarning($"[RemotePlayer] ⚠️ 角色对象不存在，无法应用外观: {Info.SteamName}");
                return;
            }

            try
            {
                Log($"[RemotePlayer] 🎨 开始应用缓存的外观数据: {Info.SteamName}");
                Utils.AppearanceConverter.ApplyAppearanceToCharacter(CharacterObject, _cachedAppearanceData);
                Log($"[RemotePlayer] ✅ 成功应用外观到角色: {Info.SteamName}");
            }
            catch (Exception ex)
            {
                LogError($"[RemotePlayer] ❌ 应用外观失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 通用延迟协程 - 等待角色初始化后执行操作
        /// </summary>
        private System.Collections.IEnumerator WaitAndExecute(System.Action action, string description)
        {
            Log($"[RemotePlayer] ⏳ 等待角色初始化完成 ({description})...");

            // 等待指定帧数，确保 characterModel 已初始化
            for (int i = 0; i < CHARACTER_INIT_WAIT_FRAMES; i++)
            {
                yield return null;
            }

            action?.Invoke();
        }

        /// <summary>
        /// 延迟应用所有数据（外观、装备、武器）
        /// </summary>
        private System.Collections.IEnumerator ApplyAllDataDelayed()
        {
            Log($"[RemotePlayer] ⏳ 等待角色初始化完成 (所有数据)...");

            // 等待指定帧数
            for (int i = 0; i < CHARACTER_INIT_WAIT_FRAMES; i++)
            {
                yield return null;
            }

            ApplyCachedAppearance();
            ApplyCachedEquipment();
            ApplyCachedWeapons();
        }

        #endregion

        #region Steam 头像

        /// <summary>
        /// 设置 Steam 头像纹理
        /// </summary>
        public override void SetAvatarTexture(Texture2D texture)
        {
            AvatarTexture = texture;
            Log($"[RemotePlayer] Steam 头像已设置: {Info.SteamId}");

            // 如果角色已创建,可以更新血条图标
            // TODO: 实现运行时更新血条图标的逻辑
        }

        #endregion

        #region 装备数据管理

        /// <summary>
        /// 设置完整的装备数据（加入房间时批量设置）
        /// </summary>
        public void SetEquipmentData(PlayerEquipmentData equipmentData)
        {
            if (equipmentData == null)
            {
                LogWarning($"[RemotePlayer] 装备数据为空");
                return;
            }

            _equipmentData = equipmentData.Clone(); // 克隆一份避免引用共享
            Log($"[RemotePlayer] 装备数据已设置: {Info.SteamName}, {_equipmentData.GetEquippedCount()} 件装备");
        }

        /// <summary>
        /// 更新单个装备槽位（实时更新）
        /// </summary>
        public void UpdateEquipmentSlot(EquipmentSlotType slotType, int? itemTypeId)
        {
            if (_equipmentData == null)
            {
                _equipmentData = new PlayerEquipmentData();
            }

            _equipmentData.SetEquipment(slotType, itemTypeId);

            string action = itemTypeId.HasValue && itemTypeId.Value > 0 ? "装备" : "卸下";
            Log($"[RemotePlayer] 装备更新: {Info.SteamName} {action} {slotType} (TypeID={itemTypeId})");
        }

        /// <summary>
        /// 获取装备数据
        /// </summary>
        public PlayerEquipmentData? GetEquipmentData()
        {
            return _equipmentData;
        }

        /// <summary>
        /// 获取指定槽位的装备TypeID
        /// </summary>
        public int? GetEquipmentTypeId(EquipmentSlotType slotType)
        {
            return _equipmentData?.GetEquipment(slotType);
        }

        /// <summary>
        /// 应用缓存的装备数据到角色（角色创建时调用）
        /// </summary>
        private void ApplyCachedEquipment()
        {
            if (_equipmentData == null || _equipmentData.GetEquippedCount() == 0)
            {
                Log($"[RemotePlayer] 没有缓存的装备数据需要应用");
                return;
            }

            if (CharacterObject == null)
            {
                LogWarning($"[RemotePlayer] 角色对象为空，无法应用装备");
                return;
            }

            var characterMainControl = CharacterObject.GetComponent<CharacterMainControl>();
            if (characterMainControl == null || characterMainControl.CharacterItem == null)
            {
                LogWarning($"[RemotePlayer] 角色组件无效，无法应用装备");
                return;
            }

            Log($"[RemotePlayer] 🎽 开始应用缓存的装备: {_equipmentData.GetEquippedCount()} 件");

            int successCount = 0;
            foreach (var kvp in _equipmentData.Equipment)
            {
                EquipmentSlotType slotType = kvp.Key;
                int itemTypeId = kvp.Value;

                if (itemTypeId > 0)
                {
                    int slotHash = GetSlotHash(slotType);
                    var slot = characterMainControl.CharacterItem.Slots.GetSlot(slotHash);

                    if (slot != null)
                    {
                        bool success = Core.Utils.EquipmentTools.CreateAndEquip(
                            itemTypeId,
                            slot,
                            HandleUnpluggedEquipment
                        );

                        if (success)
                        {
                            successCount++;
                            Log($"[RemotePlayer] ✅ 已应用装备: {slotType} = TypeID {itemTypeId}");
                        }
                    }
                }
            }

            Log($"[RemotePlayer] 🎽 装备应用完成: {successCount}/{_equipmentData.GetEquippedCount()}");
        }

        /// <summary>
        /// 获取槽位Hash值（使用缓存字典）
        /// </summary>
        private int GetSlotHash(EquipmentSlotType slotType)
        {
            return _equipmentSlotHashCache.TryGetValue(slotType, out int hash) ? hash : 0;
        }

        /// <summary>
        /// 处理被替换的装备（销毁）
        /// </summary>
        private void HandleUnpluggedEquipment(Item item)
        {
            if (item != null)
            {
                item.DestroyTree();
            }
        }

        #endregion

        #region 武器数据管理

        /// <summary>
        /// 设置武器数据（批量更新，加入房间时）
        /// </summary>
        public void SetWeaponData(PlayerWeaponData weaponData)
        {
            if (weaponData == null)
            {
                LogWarning($"[RemotePlayer] 武器数据为空");
                return;
            }

            _weaponData = weaponData; // 直接使用（服务器已经是新实例）
            Log($"[RemotePlayer] 武器数据已设置: {Info.SteamName}, {_weaponData.GetEquippedCount()} 件武器");
        }

        /// <summary>
        /// 更新单个武器槽位（增量更新）
        /// </summary>
        public void UpdateWeaponSlot(WeaponSlotType slotType, WeaponItemData? weaponData)
        {
            if (_weaponData == null)
            {
                _weaponData = new PlayerWeaponData();
            }

            _weaponData.SetWeapon(slotType, weaponData);

            string action = weaponData != null ? "装备" : "卸下";
            string weaponName = weaponData?.ItemName ?? "无";
            Log($"[RemotePlayer] 武器更新: {Info.SteamName} {action} {slotType} ({weaponName})");
        }

        /// <summary>
        /// 获取武器数据
        /// </summary>
        public PlayerWeaponData? GetWeaponData()
        {
            return _weaponData;
        }

        /// <summary>
        /// 获取指定槽位的武器数据
        /// </summary>
        public WeaponItemData? GetWeaponItemData(WeaponSlotType slotType)
        {
            return _weaponData?.GetWeapon(slotType);
        }

        /// <summary>
        /// 应用缓存的武器（角色创建后调用）
        /// </summary>
        private void ApplyCachedWeapons()
        {
            if (_weaponData == null || _weaponData.GetEquippedCount() == 0)
            {
                Log($"[RemotePlayer] 没有缓存的武器数据需要应用");
                return;
            }

            if (CharacterObject == null)
            {
                LogWarning($"[RemotePlayer] 角色对象为空，无法应用武器");
                return;
            }

            var characterMainControl = CharacterObject.GetComponent<CharacterMainControl>();
            if (characterMainControl == null || characterMainControl.CharacterItem == null)
            {
                LogWarning($"[RemotePlayer] 角色组件无效，无法应用武器");
                return;
            }

            Log($"[RemotePlayer] 🔫 开始应用缓存的武器: {_weaponData.GetEquippedCount()} 件");

            int successCount = 0;
            var weaponSlots = new[]
            {
                (WeaponSlotType.PrimaryWeapon, _weaponData.PrimaryWeapon),
                (WeaponSlotType.SecondaryWeapon, _weaponData.SecondaryWeapon),
                (WeaponSlotType.MeleeWeapon, _weaponData.MeleeWeapon)
            };

            foreach (var (slotType, weaponData) in weaponSlots)
            {
                if (weaponData != null && weaponData.ItemTypeId > 0)
                {
                    int slotHash = GetWeaponSlotHash(slotType);
                    var slot = characterMainControl.CharacterItem.Slots.GetSlot(slotHash);

                    if (slot != null)
                    {
                        // 反序列化武器数据并装备
                        Item? weaponItem = Services.WeaponSyncHelper.DeserializeItem(
                            weaponData.ItemDataCompressed,
                            weaponData.ItemTypeId
                        );

                        if (weaponItem != null)
                        {
                            bool success = slot.Plug(weaponItem, out Item unpluggedItem);
                            if (success)
                            {
                                successCount++;
                                Log($"[RemotePlayer] ✅ 武器已应用: {slotType} = {weaponData.ItemName}");

                                // 处理被替换的武器
                                if (unpluggedItem != null)
                                {
                                    unpluggedItem.DestroyTree();
                                }
                            }
                            else
                            {
                                LogWarning($"[RemotePlayer] ⚠️ 武器装备失败: {slotType}");
                                weaponItem.DestroyTree();
                            }
                        }
                        else
                        {
                            LogWarning($"[RemotePlayer] ⚠️ 武器反序列化失败: {slotType}");
                        }
                    }
                }
            }

            Log($"[RemotePlayer] 🔫 武器应用完成: {successCount}/{_weaponData.GetEquippedCount()}");
        }

        /// <summary>
        /// 获取武器槽位Hash值（使用缓存字典）
        /// </summary>
        private int GetWeaponSlotHash(WeaponSlotType slotType)
        {
            return _weaponSlotHashCache.TryGetValue(slotType, out int hash) ? hash : 0;
        }

        /// <summary>
        /// 切换武器槽位（显示对应的武器）
        /// </summary>
        public void SwitchWeaponSlot(WeaponSlotType slotType)
        {
            try
            {
                Log($"[RemotePlayer] 🔫 切换武器槽位: {Info.SteamName} → {slotType}");

                if (CharacterObject == null)
                {
                    LogWarning($"[RemotePlayer] 角色对象为空，无法切换武器");
                    return;
                }

                var characterMainControl = CharacterObject.GetComponent<CharacterMainControl>();
                if (characterMainControl == null || characterMainControl.CharacterItem == null)
                {
                    LogWarning($"[RemotePlayer] 角色组件无效，无法切换武器");
                    return;
                }

                // 更新当前武器槽位
                if (_weaponData != null)
                {
                    _weaponData.CurrentWeaponSlot = slotType;
                }

                // 获取对应槽位的武器数据
                var weaponData = _weaponData?.GetWeapon(slotType);
                if (weaponData == null || weaponData.ItemTypeId == 0)
                {
                    Log($"[RemotePlayer] 槽位 {slotType} 没有武器，清除手持武器");
                    characterMainControl.ChangeHoldItem(null);
                    return;
                }

                // 从角色的槽位中获取武器Item
                int slotHash = GetWeaponSlotHash(slotType);
                var slot = characterMainControl.CharacterItem.Slots.GetSlot(slotHash);

                if (slot == null || slot.Content == null)
                {
                    LogWarning($"[RemotePlayer] 槽位 {slotType} 中没有武器Item");
                    return;
                }

                // 调用 ChangeHoldItem 显示武器
                try
                {
                    characterMainControl.ChangeHoldItem(slot.Content);
                    Log($"[RemotePlayer] ✅ 已切换到武器: {slotType} ({weaponData.ItemName})");
                }
                catch (Exception ex)
                {
                    LogWarning($"[RemotePlayer] ChangeHoldItem 失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[RemotePlayer] 切换武器槽位失败: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// 销毁角色（覆盖基类方法以清理缓存）
        /// </summary>
        public override void DestroyCharacter()
        {
            // 清除所有角色相关的缓存
            _characterTransform = null;
            _smoothSyncManager = null;
            _cachedHealth = null;
            _cachedSetHealthMethod = null;
            
            // 调用基类方法销毁角色对象
            base.DestroyCharacter();
        }

        /// <summary>
        /// 释放资源（离开房间时调用）
        /// </summary>
        public override void Dispose()
        {
            Log($"[RemotePlayer] 远程玩家销毁（房间层）: {Info.SteamId}");
            _characterTransform = null; // 清除 Transform 缓存
            _smoothSyncManager = null;  // 清除同步管理器
            _cachedHealth = null;       // 清除 Health 缓存
            _cachedSetHealthMethod = null; // 清除 SetHealth 方法缓存
            _eventSubscriber.Dispose();  // 取消事件订阅
            base.Dispose(); // 会自动销毁角色对象
        }
    }
}