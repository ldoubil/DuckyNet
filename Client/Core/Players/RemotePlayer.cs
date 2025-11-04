using System;
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
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
        private SmoothSyncManager? _smoothSyncManager;
        private Transform? _characterTransform; // 缓存 Transform 引用
        private CharacterAppearanceData? _cachedAppearanceData; // 缓存外观数据
        private PlayerEquipmentData? _equipmentData; // 缓存装备数据
        
        /// <summary>
        /// 远程玩家当前所在的场景名称
        /// </summary>
        public string? CurrentSceneName { get; private set; }
        
        public RemotePlayer(PlayerInfo info) : base(info)
        {
            Log($"[RemotePlayer] 远程玩家创建（房间层）: {info.SteamName} ({info.SteamId})");
            
            // 🔥 初始化场景名称（从 PlayerInfo 获取）
            if (info.CurrentScenelData != null && !string.IsNullOrEmpty(info.CurrentScenelData.SceneName))
            {
                CurrentSceneName = info.CurrentScenelData.SceneName;
                Log($"[RemotePlayer] 初始场景: {CurrentSceneName}");
            }
            else
            {
                Log($"[RemotePlayer] 玩家 {info.SteamName} 初始场景未设置");
            }
            
            _eventSubscriber.EnsureInitializedAndSubscribe();
            
            // 🔥 订阅位置同步事件
            _eventSubscriber.Subscribe<PlayerUnitySyncEvent>(OnPlayerUnitySyncReceived);
            
            // 🔥 订阅场景事件（远程玩家进入/离开场景）
            _eventSubscriber.Subscribe<PlayerEnteredSceneEvent>(OnPlayerEnteredScene);
            _eventSubscriber.Subscribe<PlayerLeftSceneEvent>(OnPlayerLeftScene);
            
            // 🔥 订阅本地场景切换事件（清理已销毁的角色引用）
            _eventSubscriber.Subscribe<SceneLoadedDetailEvent>(OnLocalSceneLoaded);
            
            // 🔥 订阅外观接收事件
            _eventSubscriber.Subscribe<Services.CharacterAppearanceReceivedEvent>(OnAppearanceReceived);
            
            // 🔥 请求该玩家的外观数据
            Log($"[RemotePlayer] 🎨 远程玩家创建完成，准备请求外观数据: {info.SteamName}");
            RequestAppearanceData();
        }
        
        /// <summary>
        /// 远程玩家进入场景 - 记录场景名称
        /// </summary>
        private void OnPlayerEnteredScene(PlayerEnteredSceneEvent @event)
        {
            // 只处理自己的场景事件
            if (@event.PlayerInfo.SteamId != Info.SteamId) return;

            CurrentSceneName = @event.ScenelData.SceneName;
            Info.CurrentScenelData = @event.ScenelData; // 同步更新 PlayerInfo
            Log($"[RemotePlayer] 玩家 {Info.SteamName} 进入场景: {CurrentSceneName}");
        }

        /// <summary>
        /// 玩家离开场景 - 销毁角色
        /// </summary>
        private void OnPlayerLeftScene(PlayerLeftSceneEvent @event)
        {
            // 只处理自己的场景事件
            if (@event.PlayerInfo.SteamId != Info.SteamId) return;

            Log($"[RemotePlayer] 玩家 {Info.SteamName} 离开场景: {CurrentSceneName}");
            CurrentSceneName = null; // 清空场景名称
            Info.CurrentScenelData = new ScenelData("", ""); // 同步清空 PlayerInfo
            DestroyCharacter(); // 销毁角色，但保留 RemotePlayer
        }

        /// <summary>
        /// 本地玩家场景加载完成 - 销毁旧角色
        /// 🔥 策略：每次切换场景都重新创建角色，不移动旧模型
        /// 原因：
        /// 1. 避免场景依赖问题（角色预制体可能引用特定场景的资源）
        /// 2. 简化逻辑，不需要处理跨场景移动的复杂情况
        /// 3. 确保使用新场景的正确坐标创建角色
        /// </summary>
        private void OnLocalSceneLoaded(SceneLoadedDetailEvent @event)
        {
            // 🔥 场景切换时直接销毁旧角色，等待服务器发送新位置再重新创建
            if (!System.Object.ReferenceEquals(CharacterObject, null))
            {
                Log($"[RemotePlayer] 场景切换，销毁旧角色: {Info.SteamName}");
                UnityEngine.Object.Destroy(CharacterObject);
                CharacterObject = null;
                _characterTransform = null;
            }
            else
            {
                Log($"[RemotePlayer] 场景切换，角色引用已为空: {Info.SteamName}");
            }
            
            Log($"[RemotePlayer] 场景 {@event.ScenelData.SceneName} 加载完成，等待位置同步重建角色: {Info.SteamName}");
            
            // 🔥 不在这里重建！等服务器发送位置同步数据时，在 OnPlayerUnitySyncReceived 中创建
            // 优点：
            // 1. 使用服务器提供的准确位置
            // 2. 只创建同场景的角色（服务器已过滤）
            // 3. 角色自然地在新场景中创建，没有跨场景引用问题
        }

        /// <summary>
        /// 收到位置同步数据 - 创建或更新角色
        /// 🔥 简化逻辑：服务器已经过滤了场景，客户端直接信任服务器
        /// </summary>
        private void OnPlayerUnitySyncReceived(PlayerUnitySyncEvent @event)
        {
            // 快速过滤：检查同步数据是否是当前玩家的
            if (@event.SteamID != Info.SteamId) return;

            // 如果平滑管理器不存在，创建它
            if (_smoothSyncManager == null)
            {
                var (posX, posY, posZ) = @event.SyncData.GetPosition();
                var (rotX, rotY, rotZ, rotW) = @event.SyncData.GetRotation();
                
                _smoothSyncManager = new SmoothSyncManager(
                    new Vector3(posX, posY, posZ),
                    new Quaternion(rotX, rotY, rotZ, rotW)
                );
                
                Log($"[RemotePlayer] 初始化平滑同步管理器: {Info.SteamName}");
            }
            
            // 接收新的同步数据
            _smoothSyncManager.ReceiveSyncData(@event.SyncData);
            
            // 🔥 服务器已经过滤了场景，收到位置同步就说明在同一场景
            // 检查是否需要创建/重建角色
            bool needsRecreate = false;
            
            try
            {
                // 方法1：引用为 null（还没创建过）
                if (CharacterObject == null)
                {
                    needsRecreate = true;
                    Log($"[RemotePlayer] CharacterObject 引用为空，需要创建: {Info.SteamName}");
                }
                // 方法2：尝试访问对象属性，如果失败则说明已销毁
                else
                {
                    // Unity 特殊检查：访问 name 属性，如果抛异常说明对象已销毁
                    var _ = CharacterObject.name;
                    
                    // 额外检查：对象是否真的存在于场景中
                    if (CharacterObject == null) // Unity 的 == 运算符会返回 true 如果对象被销毁
                    {
                        needsRecreate = true;
                        Log($"[RemotePlayer] CharacterObject 已被销毁（Unity operator==），需要重建: {Info.SteamName}");
                        CharacterObject = null;
                        _characterTransform = null;
                    }
                }
            }
            catch (Exception)
            {
                // 访问对象属性失败，说明对象已销毁
                needsRecreate = true;
                Log($"[RemotePlayer] CharacterObject 访问失败（已销毁），需要重建: {Info.SteamName}");
                CharacterObject = null;
                _characterTransform = null;
            }
            
            if (needsRecreate)
            {
                var spawnPosition = _smoothSyncManager.GetPosition();
                Log($"[RemotePlayer] 🔥 收到位置同步，创建角色: {Info.SteamName} 位置: {spawnPosition}");
                CreateCharacter(spawnPosition, Info.SteamName);
            }
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
            }
            
            if (_characterTransform == null) return;
            
            // 更新平滑值
            _smoothSyncManager.Update();
            
            // 应用到角色对象（位置和旋转都应用到根Transform）
            _smoothSyncManager.ApplyToTransform(_characterTransform, _characterTransform);
        }

        /// <summary>
        /// 获取生成位置 - 可以从玩家信息中获取，或使用默认位置
        /// </summary>
        private Vector3 GetSpawnPosition()
        {
            // TODO: 从服务器同步的位置信息获取
            // 暂时使用默认位置
            return Vector3.zero;
        }

        /// <summary>
        /// 创建角色对象
        /// </summary>
        /// <param name="position">生成位置</param>
        /// <param name="displayName">显示名称（可选，默认使用 Info.SteamName）</param>
        /// <returns>创建成功返回true</returns>
        public bool CreateCharacter(Vector3 position, string? displayName = null)
        {
            // 🔥 如果未提供显示名称,使用 Info.SteamName
            displayName ??= Info.SteamName;
            
            // 如果已经有角色对象,先销毁
            if (CharacterObject != null)
            {
                DestroyCharacter();
            }

            try
            {
                // 创建角色数据项
                var characterItem = CharacterCreationUtils.CreateCharacterItem();
                if (characterItem == null)
                {
                    LogWarning($"[RemotePlayer] ⚠️ 创建角色数据项失败: {displayName}");
                    return false;
                }

                // 获取角色模型预制体
                var modelPrefab = CharacterCreationUtils.GetCharacterModelPrefab();
                if (modelPrefab == null)
                {
                    LogWarning($"[RemotePlayer] ⚠️ 获取角色模型预制体失败（可能是场景切换中 LevelManager 未就绪）: {displayName}");
                    return false;
                }

                // 实例化角色
                var newCharacter = CharacterCreationUtils.CreateCharacterInstance(
                    characterItem, modelPrefab, position, Quaternion.identity
                );
                if (newCharacter == null)
                {
                    LogWarning($"[RemotePlayer] ⚠️ 实例化角色失败: {displayName}");
                    return false;
                }

                // 配置角色基本属性
                CharacterCreationUtils.ConfigureCharacter(newCharacter, $"Character_{Info.SteamName}", position, team: 0);
                CharacterCreationUtils.ConfigureCharacterPreset(newCharacter, displayName, showName: true);
                
                // 标记为远程玩家 - 让 Movement 补丁识别并跳过移动更新
                CharacterCreationUtils.MarkAsRemotePlayer(newCharacter);
                
                // 🔥 从距离管理系统中移除 - 防止在户外场景被自动禁用
                CharacterCreationUtils.UnregisterFromDistanceSystem(newCharacter);

                // 获取自定义图标并请求血条
                var customIcon = GetCustomIcon();
                CharacterCreationUtils.RequestHealthBar(newCharacter, displayName, customIcon);

                // 保存 GameObject 引用
                Component? characterComponent = newCharacter as Component;
                if (characterComponent != null)
                {
                    CharacterObject = characterComponent.gameObject;
                    _characterTransform = CharacterObject.transform; // 立即缓存 Transform
                    
                    // 🔥 确保 GameObject 激活状态
                    if (!CharacterObject.activeSelf)
                    {
                        LogWarning($"[RemotePlayer] ⚠️ GameObject 未激活，强制激活");
                        CharacterObject.SetActive(true);
                    }
                    
                    // 🔥 验证角色在正确的场景中（只记录日志，不移动）
                    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    Log($"[RemotePlayer] 角色创建在场景: {CharacterObject.scene.name} (活动场景: {activeScene.name})");
                    Log($"[RemotePlayer] GameObject 激活状态: {CharacterObject.activeSelf}, activeInHierarchy: {CharacterObject.activeInHierarchy}");
                    
                    // 🔥 初始化平滑同步管理器（如果还没有）
                    // 注意：网络同步场景下,平滑管理器已在 OnPlayerUnitySyncReceived 中创建
                    // 这里只处理手动创建（Debug模块）的情况
                    if (_smoothSyncManager == null)
                    {
                        _smoothSyncManager = new SmoothSyncManager(
                            _characterTransform.position,
                            _characterTransform.rotation
                        );
                        Log($"[RemotePlayer] 创建平滑同步管理器: 位置 {_characterTransform.position}");
                    }
                    else
                    {
                        Log($"[RemotePlayer] 平滑管理器已存在，将通过网络同步自动更新位置");
                        Log($"[RemotePlayer]   - 管理器位置: {_smoothSyncManager.GetPosition()}");
                        Log($"[RemotePlayer]   - 角色创建位置: {_characterTransform.position}");
                    }
                    
                    // 🔥 延迟应用装备数据（等待角色初始化）
                    if (ModBehaviour.Instance != null)
                    {
                        ModBehaviour.Instance.StartCoroutine(ApplyCachedEquipmentDelayed());
                    }
                    else
                    {
                        // 直接应用
                        ApplyCachedEquipment();
                    }
                    
                    // 打印角色位置信息
                    Vector3 characterPosition = _characterTransform.position;
                    Log($"[RemotePlayer] ✅ 角色创建成功: {displayName}, 位置: {characterPosition}");
                    Log($"[RemotePlayer] GameObject Layer: {CharacterObject.layer} ({LayerMask.LayerToName(CharacterObject.layer)})");
                    
                    // 🎯 发布角色创建事件（用于动画同步注册）
                    if (GameContext.IsInitialized && GameContext.Instance.EventBus != null)
                    {
                        GameContext.Instance.EventBus.Publish(
                            new RemoteCharacterCreatedEvent(Info.SteamId, CharacterObject)
                        );
                        Log($"[RemotePlayer] 🎬 发布角色创建事件: {Info.SteamId}");
                    }
                    
                    // 🔥 检查所有子对象的激活状态
                    var renderers = CharacterObject.GetComponentsInChildren<UnityEngine.Renderer>(true);
                    Log($"[RemotePlayer] 找到 {renderers.Length} 个渲染器");
                    foreach (var renderer in renderers)
                    {
                        Log($"[RemotePlayer]   - Renderer: {renderer.name}, enabled: {renderer.enabled}, active: {renderer.gameObject.activeSelf}");
                    }
                    
                    // 🔥 打印本地玩家位置用于对比
                    if (GameContext.IsInitialized && GameContext.Instance.PlayerManager?.LocalPlayer?.CharacterObject != null)
                    {
                        var localPos = GameContext.Instance.PlayerManager.LocalPlayer.CharacterObject.transform.position;
                        float distance = Vector3.Distance(localPos, characterPosition);
                        Log($"[RemotePlayer] 本地玩家位置: {localPos}, 距离远程玩家: {distance:F2}米");
                    }
                    
                    // 🔥 角色创建成功后，延迟应用外观数据（等待 characterModel 初始化）
                    if (_cachedAppearanceData != null)
                    {
                        Log($"[RemotePlayer] 🎨 角色创建完成，延迟应用缓存的外观数据: {displayName}");
                        // 使用 ModBehaviour 的协程来延迟应用
                        if (ModBehaviour.Instance != null)
                        {
                            ModBehaviour.Instance.StartCoroutine(ApplyCachedAppearanceDelayed());
                        }
                        else
                        {
                            // 如果 ModBehaviour 不可用，直接应用（可能失败）
                            LogWarning($"[RemotePlayer] ⚠️ ModBehaviour 不可用，立即应用外观（可能失败）");
                            ApplyCachedAppearance();
                        }
                    }
                    else
                    {
                        Log($"[RemotePlayer] ⚠️ 角色创建完成，但没有缓存的外观数据: {displayName}");
                    }
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError($"[RemotePlayer] ❌ 创建角色时发生异常: {displayName}, 错误: {ex.Message}\n{ex.StackTrace}");
                return false;
            }

            return false;
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
        /// 延迟应用外观数据（等待角色初始化完成）
        /// </summary>
        private System.Collections.IEnumerator ApplyCachedAppearanceDelayed()
        {
            Log($"[RemotePlayer] ⏳ 等待角色初始化完成...");
            
            // 等待 2 帧，确保 characterModel 已初始化
            yield return null;
            yield return null;
            
            ApplyCachedAppearance();
        }

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
        /// 延迟应用装备数据
        /// </summary>
        private System.Collections.IEnumerator ApplyCachedEquipmentDelayed()
        {
            Log($"[RemotePlayer] ⏳ 等待角色初始化完成（装备系统）...");
            
            // 等待 2 帧，确保 characterModel 已初始化
            yield return null;
            yield return null;
            
            ApplyCachedEquipment();
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
        /// 获取槽位Hash值
        /// </summary>
        private int GetSlotHash(EquipmentSlotType slotType)
        {
            return slotType switch
            {
                EquipmentSlotType.Armor => CharacterEquipmentController.armorHash,
                EquipmentSlotType.Helmet => CharacterEquipmentController.helmatHash,
                EquipmentSlotType.FaceMask => CharacterEquipmentController.faceMaskHash,
                EquipmentSlotType.Backpack => CharacterEquipmentController.backpackHash,
                EquipmentSlotType.Headset => CharacterEquipmentController.headsetHash,
                _ => 0
            };
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

        /// <summary>
        /// 释放资源（离开房间时调用）
        /// </summary>
        public override void Dispose()
        {
            Log($"[RemotePlayer] 远程玩家销毁（房间层）: {Info.SteamId}");
            _characterTransform = null; // 清除 Transform 缓存
            _smoothSyncManager = null;  // 清除同步管理器
            _eventSubscriber.Dispose();  // 取消事件订阅
            base.Dispose(); // 会自动销毁角色对象
        }
    }
}