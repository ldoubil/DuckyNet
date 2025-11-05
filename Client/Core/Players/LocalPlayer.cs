using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.EventBus;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Shared.Data;
using Unity.VisualScripting;
using DuckyNet.Client.RPC;
using DuckyNet.Shared.Services.Generated;

namespace DuckyNet.Client.Core.Players
{
    /// <summary>
    /// 本地玩家管理器
    /// 负责管理本地玩家信息，包括从 Steam API 获取玩家数据
    /// </summary>
    public class LocalPlayer : BasePlayer
    {
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
        private CharacterMainControl? _characterMainControl;
        private ClientServerContext? _serverContext;
        private PlayerUnitySyncServiceClientProxy? _playerService;
        private SceneServiceClientProxy? _sceneServiceClient;
        private Shared.Services.Generated.HealthSyncServiceClientProxy? _healthSyncService;

        // 位置同步相关
        private Vector3 _lastSyncedPosition;
        private Quaternion _lastSyncedRotation;
        private Vector3 _lastSyncedVelocity;
        private Vector3 _lastFramePosition; // 🔥 上一帧位置（用于计算速度）
        private float _lastFrameTime;       // 🔥 上一帧时间
        private float _positionThreshold = 0.01f; // 1cm 移动阈值
        private float _rotationThreshold = 0.5f; // 0.5度旋转阈值
        private float _velocityThreshold = 0.1f; // 0.1 m/s 速度阈值

        // 主线程定时同步相关
        private float _syncInterval = 0.05f; // 50ms 同步间隔 (20 times/sec)
        private float _syncTimer = 0f; // 同步计时器
        private uint _sequenceNumber = 0; // 同步包序列号
        private bool _isSyncEnabled = false; // 是否启用同步

        // 血量同步相关
        private float _lastSyncedHealth = -1f; // 上次同步的血量值
        private float _lastSyncedMaxHealth = -1f; // 上次同步的最大血量值
        private float _healthThreshold = 0.5f; // 血量变化阈值（0.5 点）

        public LocalPlayer(PlayerInfo info) : base(info)
        {

            _eventSubscriber.EnsureInitializedAndSubscribe();
            _eventSubscriber.Subscribe<SceneLoadedDetailEvent>(OnSceneLoaded);
            _eventSubscriber.Subscribe<SceneUnloadingDetailEvent>(OnSceneUnloading);
            _eventSubscriber.Subscribe<RoomJoinedEvent>(OnRoomJoined);
            _eventSubscriber.Subscribe<RoomLeftEvent>(OnRoomLeft);
            // 加入场景
            _eventSubscriber.Subscribe<PlayerEnteredSceneEvent>(OnPlayerEnteredScene);
            _eventSubscriber.Subscribe<PlayerLeftSceneEvent>(OnPlayerLeftScene);
            _eventSubscriber.Subscribe<LocalPlayerShootEvent>(OnLocalPlayerShoot);
            _eventSubscriber.Subscribe<BeforeDamageAppliedEvent>(OnBeforeDamageApplied);
            
            // 订阅血量相关事件
            _eventSubscriber.Subscribe<HealthChangedEvent>(OnHealthChanged);
            _eventSubscriber.Subscribe<MaxHealthChangedEvent>(OnMaxHealthChanged);
            _eventSubscriber.Subscribe<CharacterHurtEvent>(OnCharacterHurt);
            _eventSubscriber.Subscribe<CharacterDeadEvent>(OnCharacterDead);
            
            Initialize();
        }

        #region 血量事件处理

        /// <summary>
        /// 血量变化事件处理器
        /// </summary>
        private void OnHealthChanged(HealthChangedEvent @event)
        {
            // 只处理本地玩家的血量变化
            if (!@event.IsLocalPlayer) return;

            try
            {
                // 🔥 去重：只在血量真正变化时才同步
                float healthDelta = Math.Abs(@event.CurrentHealth - _lastSyncedHealth);
                float maxHealthDelta = Math.Abs(@event.MaxHealth - _lastSyncedMaxHealth);
                
                // 如果血量或最大血量变化超过阈值，才同步
                if (healthDelta >= _healthThreshold || maxHealthDelta >= _healthThreshold)
                {
                    UnityEngine.Debug.Log($"[LocalPlayer] 💚 血量变化: {_lastSyncedHealth:F0}/{_lastSyncedMaxHealth:F0} → {@event.CurrentHealth:F0}/{@event.MaxHealth:F0}");
                    
                    // 同步血量到服务器
                    SyncHealthToServer(@event.CurrentHealth, @event.MaxHealth, false);
                    
                    // 更新缓存
                    _lastSyncedHealth = @event.CurrentHealth;
                    _lastSyncedMaxHealth = @event.MaxHealth;
                }
                // else: 血量变化太小，跳过同步（减少网络流量）
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] 处理血量变化事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 最大血量变化事件处理器
        /// </summary>
        private void OnMaxHealthChanged(MaxHealthChangedEvent @event)
        {
            // 只处理本地玩家的最大血量变化
            if (!@event.IsLocalPlayer) return;

            try
            {
                UnityEngine.Debug.Log($"[LocalPlayer] 💪 最大血量变化: {@event.MaxHealth:F0}");
                
                // TODO: 同步最大血量到服务器（如果需要）
                // SyncMaxHealthToServer(@event.MaxHealth);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] 处理最大血量变化事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 角色受伤事件处理器
        /// </summary>
        private void OnCharacterHurt(CharacterHurtEvent @event)
        {
            // 只处理本地玩家受伤
            if (!@event.IsLocalPlayer) return;

            try
            {
                UnityEngine.Debug.Log($"[LocalPlayer] 🩸 受伤: 剩余血量 {@event.CurrentHealth:F0}/{@event.MaxHealth:F0}");
                
                // TODO: 通知服务器玩家受伤（如果需要）
                // NotifyServerPlayerHurt(@event.DamageInfo, @event.CurrentHealth);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] 处理受伤事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 角色死亡事件处理器
        /// </summary>
        private void OnCharacterDead(CharacterDeadEvent @event)
        {
            // 只处理本地玩家死亡
            if (!@event.IsLocalPlayer) return;

            try
            {
                UnityEngine.Debug.Log($"[LocalPlayer] 💀 本地玩家死亡");
                
                // 通知服务器玩家死亡（同步血量为 0，无条件发送）
                SyncHealthToServer(0, 0, true);
                
                // 更新缓存（避免死亡后的血量变化再次触发同步）
                _lastSyncedHealth = 0;
                _lastSyncedMaxHealth = 0;
                
                // 停止位置同步
                StopMainThreadSync();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] 处理死亡事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 同步血量到服务器
        /// </summary>
        private void SyncHealthToServer(float currentHealth, float maxHealth, bool isDead)
        {
            try
            {
                // 检查是否已加入房间
                if (!GameContext.IsInitialized || GameContext.Instance.RoomManager?.CurrentRoom == null)
                {
                    return;
                }

                // 检查血量同步服务是否已初始化
                if (_healthSyncService == null)
                {
                    // 尝试延迟初始化
                    if (_serverContext != null)
                    {
                        _healthSyncService = new Shared.Services.Generated.HealthSyncServiceClientProxy(_serverContext);
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[LocalPlayer] 血量同步服务未初始化");
                        return;
                    }
                }

                // 创建血量同步数据
                var healthData = new Shared.Data.HealthSyncData
                {
                    SteamId = Info.SteamId,
                    CurrentHealth = currentHealth,
                    MaxHealth = maxHealth,
                    IsDead = isDead
                };

                // 发送到服务器
                _healthSyncService.SendHealthSync(healthData);

                UnityEngine.Debug.Log($"[LocalPlayer] 📤 已发送血量同步: {currentHealth:F0}/{maxHealth:F0} (死亡:{isDead})");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] 同步血量到服务器失败: {ex.Message}");
            }
        }

        #endregion

        private void OnBeforeDamageApplied(BeforeDamageAppliedEvent @event)
        {
            // 判断受伤的是否是当前 LocalPlayer 实例的角色
   
        }

        private void OnPlayerLeftScene(PlayerLeftSceneEvent @event)
        {

        }

        private void OnPlayerEnteredScene(PlayerEnteredSceneEvent @event)
        {
            if (@event.PlayerInfo.SteamId != Info.SteamId)
            {
                return;
            }
            if (CharacterObject != null && !string.IsNullOrEmpty(Info.CurrentScenelData.SceneName))
            {
                SendImmediatePositionSync();

                // 如果角色已创建，立即上传外观数据
                UploadAppearanceData();

                // 🔥 立即上传装备数据和武器数据
                UploadEquipmentData();
                UploadWeaponData();
            }
        }

        /// <summary>
        /// 本地玩家开枪事件处理器
        /// </summary>
        private void OnLocalPlayerShoot(LocalPlayerShootEvent evt)
        {
            try
            {
                // 获取枪械名称
                string gunName = "Unknown";
                if (evt.Gun is Component gunComponent)
                {
                    gunName = gunComponent.gameObject.name;
                }

            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] 处理开枪事件失败: {ex.Message}");
            }
        }

        private void OnRoomJoined(RoomJoinedEvent @event)
        {
            if (@event.Player.SteamId != Info.SteamId)
            {
                return;
            }
            UnityEngine.Debug.Log($"[LocalPlayer] 加入房间: {@event.Room.RoomId}，启动位置同步");

            // 这样其他玩家加入房间时,服务器缓存中就有我的位置了
            if (CharacterObject != null && !string.IsNullOrEmpty(Info.CurrentScenelData.SceneName))
            {
                SendImmediatePositionSync();

                // 如果角色已创建，立即上传外观数据
                UploadAppearanceData();

                // 🔥 立即上传装备数据和武器数据
                UploadEquipmentData();
                UploadWeaponData();
            }
            // 发送加入场景
            if (Info.CurrentScenelData.SceneName != "" && Info.CurrentScenelData.SubSceneName != "")
            {
                _sceneServiceClient?.EnterSceneAsync(Info.CurrentScenelData);
            }

            StartMainThreadSync();
        }

        private void OnRoomLeft(RoomLeftEvent @event)
        {
            if (@event.Player.SteamId != Info.SteamId)
            {
                return;
            }
            UnityEngine.Debug.Log($"[LocalPlayer] 离开房间: {@event.Room.RoomId}，停止位置同步");
            StopMainThreadSync();
        }

        private void OnSceneUnloading(SceneUnloadingDetailEvent @event)
        {
            _sceneServiceClient?.LeaveSceneAsync(Info.CurrentScenelData);
            Info.CurrentScenelData = new ScenelData("", "");

            // 🔥 修复：更新 RoomManager.RoomPlayers 中自己的场景信息
            if (GameContext.IsInitialized && GameContext.Instance.RoomManager != null)
            {
                var myself = GameContext.Instance.RoomManager.RoomPlayers.Find(p => p.SteamId == Info.SteamId);
                if (myself != null)
                {
                    myself.CurrentScenelData = new ScenelData("", "");
                    UnityEngine.Debug.Log($"[LocalPlayer] ✅ 已清空房间列表中自己的场景信息");
                }
            }
        }

        private void OnSceneLoaded(SceneLoadedDetailEvent @event)
        {
            Info.CurrentScenelData = @event.ScenelData;
            CharacterObject = CharacterMainControl.Main?.gameObject;
            _characterMainControl = CharacterMainControl.Main;

            // 重置上次同步的位置信息
            if (CharacterObject != null)
            {
                _lastSyncedPosition = CharacterObject.transform.position;
                _lastSyncedRotation = CharacterObject.transform.rotation;
                _lastFramePosition = _lastSyncedPosition; // 🔥 初始化
                _lastFrameTime = Time.time;
            }

            // 🔥 修复：更新 RoomManager.RoomPlayers 中自己的场景信息
            if (GameContext.IsInitialized && GameContext.Instance.RoomManager != null)
            {
                var myself = GameContext.Instance.RoomManager.RoomPlayers.Find(p => p.SteamId == Info.SteamId);
                if (myself != null)
                {
                    myself.CurrentScenelData = @event.ScenelData;
                }
            }
            _sceneServiceClient?.EnterSceneAsync(Info.CurrentScenelData);
            if (CharacterObject != null)
            {
                UploadAppearanceData();
                UploadEquipmentData();
                UploadWeaponData();
            }



            // 注意：不在这里启动同步，由加入房间事件触发
        }

        /// <summary>
        /// 从 Steam API 初始化玩家信息
        /// </summary>
        private void Initialize()
        {
            try
            {
                // 延迟初始化 RPC 客户端（在 GameContext 完全初始化后）
                if (GameContext.IsInitialized && GameContext.Instance.RpcClient != null)
                {
                    _serverContext = new ClientServerContext(GameContext.Instance.RpcClient);
                    _playerService = new PlayerUnitySyncServiceClientProxy(_serverContext);
                    _sceneServiceClient = new SceneServiceClientProxy(_serverContext);
                    _healthSyncService = new Shared.Services.Generated.HealthSyncServiceClientProxy(_serverContext);
                    UnityEngine.Debug.Log($"[LocalPlayer] RPC 客户端已初始化");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] GameContext 未初始化或 RpcClient 为空");
                }

                if (!SteamManager.Initialized)
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] Steam 未初始化，使用默认玩家信息");
                    InitializeWithDefaultInfo();
                    return;
                }

                // 从 Steam 获取玩家信息
                CSteamID steamId = SteamUser.GetSteamID();
                string steamUsername = SteamFriends.GetPersonaName();
                string avatarUrl = GetSteamAvatarUrl(steamId);

                Info = new PlayerInfo
                {
                    SteamId = steamId.ToString(),
                    SteamName = steamUsername,
                    AvatarUrl = avatarUrl,
                };
                // 异步加载头像纹理
                LoadAvatarTexture(steamId);

            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] 初始化失败: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
                InitializeWithDefaultInfo();
            }

        }

        /// <summary>
        /// 使用默认信息初始化（Steam不可用时）
        /// </summary>
        private void InitializeWithDefaultInfo()
        {
            Info = new PlayerInfo
            {
                SteamId = "default_" + Guid.NewGuid().ToString().Substring(0, 8),
                SteamName = "Player_" + UnityEngine.Random.Range(1000, 9999),
                AvatarUrl = string.Empty,
            };
        }

        /// <summary>
        /// 获取 Steam 头像 URL
        /// </summary>
        private string GetSteamAvatarUrl(CSteamID steamId)
        {
            try
            {
                // 获取中等尺寸头像
                int avatarHandle = SteamFriends.GetMediumFriendAvatar(steamId);

                if (avatarHandle == -1 || avatarHandle == 0)
                {
                    UnityEngine.Debug.LogWarning($"[LocalPlayer] 无法获取头像句柄");
                    return string.Empty;
                }
                string steamId64 = steamId.ToString();
                return $"https://steamcommunity.com/profiles/{steamId64}/";
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[LocalPlayer] 获取头像 URL 失败: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 加载 Steam 头像纹理
        /// </summary>
        private void LoadAvatarTexture(CSteamID steamId)
        {
            try
            {
                // 获取中等尺寸头像句柄
                int avatarHandle = SteamFriends.GetMediumFriendAvatar(steamId);

                if (avatarHandle == -1 || avatarHandle == 0)
                {
                    UnityEngine.Debug.LogWarning($"[LocalPlayer] 无效的头像句柄");
                    return;
                }

                // 获取头像尺寸
                bool success = SteamUtils.GetImageSize(avatarHandle, out uint width, out uint height);
                if (!success || width == 0 || height == 0)
                {
                    UnityEngine.Debug.LogWarning($"[LocalPlayer] 无法获取头像尺寸");
                    return;
                }

                // 创建纹理
                byte[] imageData = new byte[width * height * 4]; // RGBA
                success = SteamUtils.GetImageRGBA(avatarHandle, imageData, (int)(width * height * 4));

                if (!success)
                {
                    UnityEngine.Debug.LogWarning($"[LocalPlayer] 无法获取头像数据");
                    return;
                }

                // 创建 Unity 纹理
                this.AvatarTexture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                AvatarTexture.LoadRawTextureData(imageData);
                AvatarTexture.Apply();

                // 垂直翻转（Steam 图像是上下颠倒的）
                FlipTextureVertically(AvatarTexture);

                UnityEngine.Debug.Log($"[LocalPlayer] 头像纹理已加载: {width}x{height}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[LocalPlayer] 加载头像纹理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 垂直翻转纹理
        /// </summary>
        private void FlipTextureVertically(Texture2D texture)
        {
            Color[] pixels = texture.GetPixels();
            Color[] flipped = new Color[pixels.Length];

            int width = texture.width;
            int height = texture.height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    flipped[x + y * width] = pixels[x + (height - y - 1) * width];
                }
            }

            texture.SetPixels(flipped);
            texture.Apply();
        }

        /// <summary>
        /// Unity LateUpdate - 主线程定时同步位置
        /// </summary>
        public void LateUpdate()
        {
            // 如果 RPC 服务尚未初始化，尝试初始化
            if (_playerService == null && GameContext.IsInitialized && GameContext.Instance.RpcClient != null)
            {
                _serverContext = new ClientServerContext(GameContext.Instance.RpcClient);
                _playerService = new PlayerUnitySyncServiceClientProxy(_serverContext);
                UnityEngine.Debug.Log("[LocalPlayer] RPC 客户端延迟初始化成功");

                // 如果场景已加载且还没启动同步，立即启动
                if (CharacterObject != null && !_isSyncEnabled)
                {
                    UnityEngine.Debug.Log("[LocalPlayer] LateUpdate 中触发同步启动");
                    StartMainThreadSync();
                }
            }

            // 如果同步未启用，直接返回
            if (!_isSyncEnabled)
                return;

            // 累加时间
            _syncTimer += Time.deltaTime;

            // 检查是否到达同步间隔
            if (_syncTimer >= _syncInterval)
            {
                _syncTimer = 0f;
                SendPositionSync();
            }
        }

        /// <summary>
        /// 启动主线程同步
        /// </summary>
        private void StartMainThreadSync()
        {
            if (_playerService == null)
            {
                UnityEngine.Debug.LogWarning("[LocalPlayer] _playerService 未初始化，无法启动主线程同步");
                return;
            }

            UnityEngine.Debug.Log($"[LocalPlayer] 启动主线程同步循环 (间隔: {_syncInterval}s, 频率: 30/sec)");
            _isSyncEnabled = true;
            _syncTimer = 0f;
        }

        /// <summary>
        /// 停止主线程同步
        /// </summary>
        private void StopMainThreadSync()
        {
            UnityEngine.Debug.Log("[LocalPlayer] 停止主线程同步");
            _isSyncEnabled = false;
            _syncTimer = 0f;
        }

        /// <summary>
        /// 发送位置同步数据 (在主线程调用)
        /// </summary>
        private void SendPositionSync()
        {
            // ========== 检查前置条件 ==========
            if (CharacterObject == null || _playerService == null)
                return;

            // 检查是否已进入场景
            if (string.IsNullOrEmpty(Info.CurrentScenelData.SceneName) ||
                string.IsNullOrEmpty(Info.CurrentScenelData.SubSceneName))
            {
                // 未加入场景/子场景，不发送
                return;
            }

            try
            {
                // ========== 在主线程安全地读取 Unity 对象数据 ==========
                Vector3 currentPosition = CharacterObject.transform.position;

                // 🔥 使用 CharacterMainControl.CurrentAimDirection 获取角色朝向
                Quaternion currentRotation = Quaternion.identity;
                if (_characterMainControl != null)
                {
                    Vector3 aimDirection = _characterMainControl.CurrentAimDirection;
                    if (aimDirection != Vector3.zero)
                    {
                        currentRotation = Quaternion.LookRotation(aimDirection);
                    }
                }

                Vector3 currentVelocity = Vector3.zero;

                // 🔥 改进速度计算：优先使用 Rigidbody，否则手动计算
                Rigidbody rb = CharacterObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    currentVelocity = rb.velocity;
                }
                else
                {
                    // 没有 Rigidbody，通过位置差计算速度
                    float deltaTime = Time.time - _lastFrameTime;
                    if (deltaTime > 0.001f) // 防止除0
                    {
                        currentVelocity = (currentPosition - _lastFramePosition) / deltaTime;
                    }
                    _lastFramePosition = currentPosition;
                    _lastFrameTime = Time.time;
                }

                // ========== 检查数据是否有实质性变化 ==========
                float positionDelta = Vector3.Distance(currentPosition, _lastSyncedPosition);
                float rotationDelta = Quaternion.Angle(currentRotation, _lastSyncedRotation);
                float velocityDelta = Vector3.Distance(currentVelocity, _lastSyncedVelocity);

                // 如果数据变化不足阈值，跳过发送
                if (positionDelta < _positionThreshold &&
                    rotationDelta < _rotationThreshold &&
                    velocityDelta < _velocityThreshold)
                {
                    return;
                }

                // ========== 创建并发送同步数据 ==========
                UnitySyncData syncData = new UnitySyncData
                {
                    SteamId = Info.SteamId,
                    SequenceNumber = ++_sequenceNumber, // 递增序列号
                };

                // 设置位置
                syncData.SetPosition(currentPosition.x, currentPosition.y, currentPosition.z);

                // 设置旋转
                syncData.SetRotation(currentRotation.x, currentRotation.y, currentRotation.z, currentRotation.w);

                // 设置速度
                syncData.SetVelocity(currentVelocity.x, currentVelocity.y, currentVelocity.z);

                // 发送同步数据
                _playerService.SendPlayerUnitySync(syncData);

                // 更新上次同步的数据
                _lastSyncedPosition = currentPosition;
                _lastSyncedRotation = currentRotation;
                _lastSyncedVelocity = currentVelocity;

            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] 发送位置同步失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 立即发送一次位置同步（用于加入房间时）
        /// </summary>
        private void SendImmediatePositionSync()
        {
            if (_playerService == null || CharacterObject == null)
            {
                UnityEngine.Debug.LogWarning("[LocalPlayer] 无法立即发送位置同步：RPC服务或角色对象为空");
                return;
            }

            try
            {
                var currentPosition = CharacterObject.transform.position;

                // 🔥 使用 CharacterMainControl.CurrentAimDirection 获取角色朝向
                Quaternion currentRotation = Quaternion.identity;
                if (_characterMainControl != null)
                {
                    Vector3 aimDirection = _characterMainControl.CurrentAimDirection;
                    if (aimDirection != Vector3.zero)
                    {
                        currentRotation = Quaternion.LookRotation(aimDirection);
                    }
                }

                var currentVelocity = Vector3.zero;

                // 尝试获取速度
                Rigidbody rb = CharacterObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    currentVelocity = rb.velocity;
                }
                else
                {
                    // 没有Rigidbody，手动计算速度
                    if (_lastFrameTime > 0)
                    {
                        float deltaTime = Time.time - _lastFrameTime;
                        if (deltaTime > 0.001f)
                        {
                            currentVelocity = (currentPosition - _lastFramePosition) / deltaTime;
                        }
                    }
                }

                // 创建同步数据
                UnitySyncData syncData = new UnitySyncData
                {
                    SteamId = Info.SteamId,
                    SequenceNumber = ++_sequenceNumber,
                };

                syncData.SetPosition(currentPosition.x, currentPosition.y, currentPosition.z);
                syncData.SetRotation(currentRotation.x, currentRotation.y, currentRotation.z, currentRotation.w);
                syncData.SetVelocity(currentVelocity.x, currentVelocity.y, currentVelocity.z);

                // 立即发送
                _playerService.SendPlayerUnitySync(syncData);

                // 更新缓存
                _lastSyncedPosition = currentPosition;
                _lastSyncedRotation = currentRotation;
                _lastSyncedVelocity = currentVelocity;
                _lastFramePosition = currentPosition;
                _lastFrameTime = Time.time;

                UnityEngine.Debug.Log($"[LocalPlayer] 🔥 立即发送位置同步: Pos({currentPosition.x:F2},{currentPosition.y:F2},{currentPosition.z:F2}) " +
                    $"Rot(Y:{currentRotation.eulerAngles.y:F1}°) " +
                    $"场景:{Info.CurrentScenelData.SceneName}/{Info.CurrentScenelData.SubSceneName}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] 立即发送位置同步失败: {ex.Message}");
            }
        }


        public override void SetAvatarTexture(Texture2D texture)
        {
            this.AvatarTexture = texture;
        }

        /// <summary>
        /// 上传角色外观数据到服务器
        /// </summary>
        private void UploadAppearanceData()
        {
            try
            {
                UnityEngine.Debug.Log($"[LocalPlayer] 🎨 开始上传角色外观数据...");

                // 检查角色是否已创建
                if (CharacterObject == null || _characterMainControl == null)
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] ⚠️ 角色尚未创建，跳过上传外观数据");
                    return;
                }

                // 获取本地玩家外观数据
                var appearanceData = Utils.AppearanceConverter.LoadMainCharacterAppearance();
                if (appearanceData == null)
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] ❌ 无法获取角色外观数据");
                    return;
                }

                UnityEngine.Debug.Log($"[LocalPlayer] ✅ 成功获取外观数据 - HeadScale: {appearanceData.HeadSetting.ScaleX}, Parts: {appearanceData.Parts.Length}");

                // 调用 RPC 上传外观
                if (GameContext.IsInitialized && GameContext.Instance.RpcClient != null)
                {
                    UnityEngine.Debug.Log($"[LocalPlayer] 📤 正在通过RPC上传外观数据到服务器...");
                    GameContext.Instance.RpcClient.InvokeServer<Shared.Services.ICharacterAppearanceService>(
                        nameof(Shared.Services.ICharacterAppearanceService.UploadAppearance),
                        appearanceData
                    );
                    UnityEngine.Debug.Log($"[LocalPlayer] ✅ 外观数据已发送到服务器");
                }
                else
                {
                    UnityEngine.Debug.LogError("[LocalPlayer] ❌ RpcClient未初始化，无法上传外观数据");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] ❌ 上传外观数据失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 上传装备数据到服务器（加入房间时调用）
        /// </summary>
        private async void UploadEquipmentData()
        {
            try
            {
                UnityEngine.Debug.Log($"[LocalPlayer] 🎽 开始上传角色装备数据...");

                if (CharacterObject == null)
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] ⚠️ 角色尚未创建，跳过上传装备数据");
                    return;
                }

                var characterMainControl = CharacterObject.GetComponent<CharacterMainControl>();
                if (characterMainControl == null || characterMainControl.CharacterItem == null)
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] ❌ 无法获取角色装备数据");
                    return;
                }

                var characterItem = characterMainControl.CharacterItem;

                // 获取所有装备槽位
                var equipmentSlots = new[]
                {
                    (CharacterEquipmentController.armorHash, Shared.Data.EquipmentSlotType.Armor, "护甲"),
                    (CharacterEquipmentController.helmatHash, Shared.Data.EquipmentSlotType.Helmet, "头盔"),
                    (CharacterEquipmentController.faceMaskHash, Shared.Data.EquipmentSlotType.FaceMask, "面罩"),
                    (CharacterEquipmentController.backpackHash, Shared.Data.EquipmentSlotType.Backpack, "背包"),
                    (CharacterEquipmentController.headsetHash, Shared.Data.EquipmentSlotType.Headset, "耳机")
                };

                if (_serverContext == null)
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] ❌ RPC上下文未初始化，无法上传装备数据");
                    return;
                }

                // 创建装备服务代理
                var equipmentService = new Shared.Services.Generated.EquipmentServiceClientProxy(_serverContext);
                int uploadedCount = 0;

                // 上传每个槽位的装备
                foreach (var (slotHash, slotType, slotName) in equipmentSlots)
                {
                    var slot = characterItem.Slots.GetSlot(slotHash);
                    int? itemTypeId = slot?.Content?.TypeID;

                    if (itemTypeId.HasValue && itemTypeId.Value > 0)
                    {
                        var request = new Shared.Data.EquipmentSlotUpdateRequest
                        {
                            SlotType = slotType,
                            ItemTypeId = itemTypeId
                        };

                        bool success = await equipmentService.UpdateEquipmentSlotAsync(request);
                        if (success)
                        {
                            uploadedCount++;
                            UnityEngine.Debug.Log($"[LocalPlayer] ✅ 已上传装备: {slotName} = TypeID {itemTypeId}");
                        }
                    }
                }

                UnityEngine.Debug.Log($"[LocalPlayer] 🎽 装备数据上传完成: {uploadedCount} 件装备");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] ❌ 上传装备数据失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 上传武器数据到服务器（加入房间时调用）
        /// </summary>
        private async void UploadWeaponData()
        {
            try
            {
                UnityEngine.Debug.Log($"[LocalPlayer] 🔫 开始上传角色武器数据...");

                if (CharacterObject == null)
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] ⚠️ 角色尚未创建，跳过上传武器数据");
                    return;
                }

                var characterMainControl = CharacterObject.GetComponent<CharacterMainControl>();
                if (characterMainControl == null || characterMainControl.CharacterItem == null)
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] ❌ 无法获取角色武器数据");
                    return;
                }

                var characterItem = characterMainControl.CharacterItem;

                // 获取所有武器槽位
                var weaponSlots = new[]
                {
                    ("PrimaryWeapon".GetHashCode(), Shared.Data.WeaponSlotType.PrimaryWeapon, "主武器"),
                    ("SecondaryWeapon".GetHashCode(), Shared.Data.WeaponSlotType.SecondaryWeapon, "副武器"),
                    ("MeleeWeapon".GetHashCode(), Shared.Data.WeaponSlotType.MeleeWeapon, "近战武器")
                };

                if (_serverContext == null)
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] ❌ RPC上下文未初始化，无法上传武器数据");
                    return;
                }

                // 创建武器服务代理
                var weaponService = new Shared.Services.Generated.WeaponSyncServiceClientProxy(_serverContext);
                int uploadedCount = 0;

                // 上传每个槽位的武器
                foreach (var (slotHash, slotType, slotName) in weaponSlots)
                {
                    var slot = characterItem.Slots.GetSlot(slotHash);
                    if (slot?.Content != null)
                    {
                        var weaponItem = slot.Content;

                        // 使用 WeaponSyncHelper 创建请求（包含序列化数据）
                        var request = Services.WeaponSyncHelper.CreateWeaponSlotUpdateRequest(slotType, weaponItem);

                        bool success = await weaponService.EquipWeaponAsync(request);
                        if (success)
                        {
                            uploadedCount++;
                            string dataInfo = request.IsDefaultItem ? "默认" : $"{request.ItemDataCompressed.Length}字节";
                            UnityEngine.Debug.Log($"[LocalPlayer] ✅ 已上传武器: {slotName} = {weaponItem.DisplayName} (数据={dataInfo})");
                        }
                    }
                }

                UnityEngine.Debug.Log($"[LocalPlayer] 🔫 武器数据上传完成: {uploadedCount} 件武器");

                // 🔥 上传当前手持的武器槽位
                await UploadCurrentWeaponSlot(characterMainControl);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] ❌ 上传武器数据失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 上传当前手持的武器槽位
        /// </summary>
        private async System.Threading.Tasks.Task UploadCurrentWeaponSlot(CharacterMainControl characterMainControl)
        {
            try
            {
                if (characterMainControl == null || characterMainControl.CurrentHoldItemAgent == null)
                {
                    UnityEngine.Debug.Log("[LocalPlayer] 当前没有手持武器，跳过槽位同步");
                    return;
                }

                var currentWeapon = characterMainControl.CurrentHoldItemAgent.Item;
                if (currentWeapon == null)
                {
                    return;
                }

                // 确定当前武器在哪个槽位
                Shared.Data.WeaponSlotType? slotType = null;

                if (characterMainControl.PrimWeaponSlot()?.Content == currentWeapon)
                    slotType = Shared.Data.WeaponSlotType.PrimaryWeapon;
                else if (characterMainControl.SecWeaponSlot()?.Content == currentWeapon)
                    slotType = Shared.Data.WeaponSlotType.SecondaryWeapon;
                else if (characterMainControl.MeleeWeaponSlot()?.Content == currentWeapon)
                    slotType = Shared.Data.WeaponSlotType.MeleeWeapon;

                if (!slotType.HasValue)
                {
                    UnityEngine.Debug.LogWarning($"[LocalPlayer] 无法确定当前武器的槽位: {currentWeapon.DisplayName}");
                    return;
                }

                if (_serverContext == null)
                {
                    UnityEngine.Debug.LogWarning("[LocalPlayer] ❌ RPC上下文未初始化，无法上传武器槽位");
                    return;
                }

                var weaponService = new Shared.Services.Generated.WeaponSyncServiceClientProxy(_serverContext);
                var request = new Shared.Data.WeaponSwitchRequest
                {
                    CurrentWeaponSlot = slotType.Value
                };

                bool success = await weaponService.SwitchWeaponSlotAsync(request);
                if (success)
                {
                    UnityEngine.Debug.Log($"[LocalPlayer] ✅ 已上传当前武器槽位: {slotType} ({currentWeapon.DisplayName})");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalPlayer] ❌ 上传当前武器槽位失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public override void Dispose()
        {
            StopMainThreadSync();
            _eventSubscriber?.Dispose();

            if (AvatarTexture != null)
            {
                UnityEngine.Object.Destroy(AvatarTexture);
                AvatarTexture = null;
            }

            // 调用基类 Dispose 销毁角色对象
            base.Dispose();
        }
    }
}

