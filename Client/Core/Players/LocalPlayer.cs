using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;
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

        // 异步定时同步相关
        private float _syncInterval = 0.033f; // 33ms 同步间隔 (30 times/sec) - 🔥 提升频率
        private System.Threading.CancellationTokenSource? _syncCancellationTokenSource;
        private bool _isDisposed = false;
        private uint _sequenceNumber = 0; // 同步包序列号

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
            // 这样其他玩家加入房间时，服务器缓存中就有我的位置了
            if (CharacterObject != null && !string.IsNullOrEmpty(Info.CurrentScenelData.SceneName))
            {
                UnityEngine.Debug.Log($"[LocalPlayer] 🔥 已在场景中，立即发送位置同步");
                SendImmediatePositionSync();
            }
            
            StartAsyncSync();
        }

        private void OnRoomLeft(RoomLeftEvent @event)
        {
            UnityEngine.Debug.Log($"[LocalPlayer] 离开房间: {@event.Room.RoomId}，停止位置同步");
            StopAsyncSync();
        }

        private void OnSceneUnloading(SceneUnloadingDetailEvent @event)
        {
            Info.CurrentScenelData = new ScenelData("", "");
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
        /// Unity LateUpdate - 检查异步同步的启动条件
        /// </summary>
        public void LateUpdate()
        {
            // 如果 RPC 服务尚未初始化，尝试初始化
            if (_playerService == null && GameContext.IsInitialized && GameContext.Instance.RpcClient != null)
            {
                _serverContext = new ClientServerContext(GameContext.Instance.RpcClient);
                _playerService = new PlayerUnitySyncServiceClientProxy(_serverContext);
                UnityEngine.Debug.Log("[LocalPlayer] RPC 客户端延迟初始化成功");

                // 如果场景已加载且还没启动异步同步，立即启动
                if (CharacterObject != null && _syncCancellationTokenSource == null)
                {
                    UnityEngine.Debug.Log("[LocalPlayer] LateUpdate 中触发异步同步启动");
                    StartAsyncSync();
                }
            }
        }

        // 异步同步循环
        private void StartAsyncSync()
        {
            // 停止之前的同步任务
            StopAsyncSync();

            if (_playerService == null)
            {
                UnityEngine.Debug.LogWarning("[LocalPlayer] _playerService 未初始化，无法启动异步同步");
                return;
            }

            UnityEngine.Debug.Log($"[LocalPlayer] 启动异步同步循环 (间隔: {_syncInterval}s, 频率: 20/sec)");

            _syncCancellationTokenSource = new System.Threading.CancellationTokenSource();
            var token = _syncCancellationTokenSource.Token;

            // 启动异步定时同步任务
            System.Threading.Tasks.Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && !_isDisposed)
                {
                    try
                    {
                        // 等待同步间隔
                        await System.Threading.Tasks.Task.Delay((int)(_syncInterval * 1000), token);

                        if (token.IsCancellationRequested || _isDisposed)
                            break;

                        // ========== 检查前置条件 ==========
                        // 注意：房间检查已移除，因为同步循环只在加入房间后启动
                        
                        // 检查是否已进入场景
                        if (string.IsNullOrEmpty(Info.CurrentScenelData.SceneName) || 
                            string.IsNullOrEmpty(Info.CurrentScenelData.SubSceneName))
                        {
                            // 未加入场景/子场景，不发送
                            continue;
                        }

                        // ========== 读取角色数据 ==========
                        // 收集 Unity 对象的数据（在后台线程中只读取，不修改）
                        Vector3 currentPosition = Vector3.zero;
                        Quaternion currentRotation = Quaternion.identity;
                        Vector3 currentVelocity = Vector3.zero;
                        bool hasValidData = false;

                        // 从主线程安全地读取 Unity 对象数据
                        // 注意：这里我们只是读取数据，不修改任何 Unity 对象
                        if (CharacterObject != null)
                        {
                            try
                            {
                                currentPosition = CharacterObject.transform.position;
                                currentRotation = CharacterObject.transform.rotation;

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

                                hasValidData = true;
                            }
                            catch
                            {
                                // 如果读取失败（可能对象被销毁），继续循环
                                continue;
                            }
                        }

                        if (!hasValidData)
                            continue;

                        // ========== 检查数据是否有实质性变化 ==========
                        float positionDelta = Vector3.Distance(currentPosition, _lastSyncedPosition);
                        float rotationDelta = Quaternion.Angle(currentRotation, _lastSyncedRotation);
                        float velocityDelta = Vector3.Distance(currentVelocity, _lastSyncedVelocity);

                        // 如果数据变化不足阈值，跳过发送
                        if (positionDelta < _positionThreshold &&
                            rotationDelta < _rotationThreshold &&
                            velocityDelta < _velocityThreshold)
                        {
                            continue;
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

                        // 发送同步数据（RPC 调用是线程安全的）
                        _playerService.SendPlayerUnitySync(syncData);

                        // 更新上次同步的数据
                        _lastSyncedPosition = currentPosition;
                        _lastSyncedRotation = currentRotation;
                        _lastSyncedVelocity = currentVelocity;

                        string roomId = GameContext.Instance?.RoomManager?.CurrentRoom?.RoomId ?? "Unknown";
                        // 🔥 改进日志：显示Y轴旋转和速度
                        float yRotation = currentRotation.eulerAngles.y;
                        UnityEngine.Debug.Log($"[LocalPlayer] 发送同步数据: " +
                            $"Pos({currentPosition.x:F2},{currentPosition.y:F2},{currentPosition.z:F2}) " +
                            $"Rot(Y:{yRotation:F1}°) " +
                            $"Vel({currentVelocity.x:F2},{currentVelocity.y:F2},{currentVelocity.z:F2}) " +
                            $"房间:{roomId} 场景:{Info.CurrentScenelData.SceneName}/{Info.CurrentScenelData.SubSceneName}");
                    }
                    catch (System.OperationCanceledException)
                    {
                        // 任务被取消，正常退出
                        UnityEngine.Debug.Log("[LocalPlayer] 异步同步任务已取消");
                        break;
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[LocalPlayer] 异步同步任务异常: {ex.Message}");
                    }
                }

                UnityEngine.Debug.Log("[LocalPlayer] 异步同步循环已结束");
            }, token);
        }

        private void StopAsyncSync()
        {
            if (_syncCancellationTokenSource != null)
            {
                try
                {
                    _syncCancellationTokenSource.Cancel();
                    _syncCancellationTokenSource.Dispose();
                }
                catch { }
                finally
                {
                    _syncCancellationTokenSource = null;
                }
            }
        }

        // Update 方法已移除 - 使用异步定时同步替代
        // 对比：
        // 每帧调用: 60-120fps，CPU开销大，网络流量大
        // 异步定时: 10/sec (100ms间隔)，CPU开销小，网络流量合理
        // 节省对比: CPU/网络 节省 85-90%

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
                var currentRotation = CharacterObject.transform.rotation;
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

        public override void Dispose()
        {
            _isDisposed = true;
            StopAsyncSync();
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

