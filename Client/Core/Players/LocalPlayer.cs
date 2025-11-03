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
        private float _syncInterval = 0.033f; // 33ms 同步间隔 (30 times/sec)
        private float _syncTimer = 0f; // 同步计时器
        private uint _sequenceNumber = 0; // 同步包序列号
        private bool _isSyncEnabled = false; // 是否启用同步

        public LocalPlayer(PlayerInfo info) : base(info)
        {

            _eventSubscriber.EnsureInitializedAndSubscribe();
            _eventSubscriber.Subscribe<SceneLoadedDetailEvent>(OnSceneLoaded);
            _eventSubscriber.Subscribe<SceneUnloadingDetailEvent>(OnSceneUnloading);
            _eventSubscriber.Subscribe<RoomJoinedEvent>(OnRoomJoined);
            _eventSubscriber.Subscribe<RoomLeftEvent>(OnRoomLeft);
            Initialize();
        }

        private void OnRoomJoined(RoomJoinedEvent @event)
        {
            UnityEngine.Debug.Log($"[LocalPlayer] 加入房间: {@event.Room.RoomId}，启动位置同步");
            
            // 🔥 关键修复：如果已经在场景中，立即发送一次位置同步
            // 这样其他玩家加入房间时,服务器缓存中就有我的位置了
            if (CharacterObject != null && !string.IsNullOrEmpty(Info.CurrentScenelData.SceneName))
            {
                UnityEngine.Debug.Log($"[LocalPlayer] 🔥 已在场景中，立即发送位置同步");
                SendImmediatePositionSync();
                
                // 如果角色已创建，立即上传外观数据
                UploadAppearanceData();
            }
            
            StartMainThreadSync();
        }

        private void OnRoomLeft(RoomLeftEvent @event)
        {
            UnityEngine.Debug.Log($"[LocalPlayer] 离开房间: {@event.Room.RoomId}，停止位置同步");
            StopMainThreadSync();
        }

        private void OnSceneUnloading(SceneUnloadingDetailEvent @event)
        {
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
                    UnityEngine.Debug.Log($"[LocalPlayer] ✅ 已更新房间列表中自己的场景信息: {@event.ScenelData.SceneName}/{@event.ScenelData.SubSceneName}");
                }
            }

            // 🔥 场景加载完成，角色已创建，上传外观数据
            if (CharacterObject != null)
            {
                UnityEngine.Debug.Log($"[LocalPlayer] 场景加载完成，角色已创建，准备上传外观数据");
                UploadAppearanceData();
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

                // 可选：输出调试日志
                // string roomId = GameContext.Instance?.RoomManager?.CurrentRoom?.RoomId ?? "Unknown";
                // float yRotation = currentRotation.eulerAngles.y;
                // UnityEngine.Debug.Log($"[LocalPlayer] 发送同步数据: " +
                //     $"Pos({currentPosition.x:F2},{currentPosition.y:F2},{currentPosition.z:F2}) " +
                //     $"Rot(Y:{yRotation:F1}°) " +
                //     $"Vel({currentVelocity.x:F2},{currentVelocity.y:F2},{currentVelocity.z:F2}) " +
                //     $"房间:{roomId} 场景:{Info.CurrentScenelData.SceneName}/{Info.CurrentScenelData.SubSceneName}");
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

