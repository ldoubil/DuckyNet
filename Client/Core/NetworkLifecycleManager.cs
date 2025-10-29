using System;
using System.Threading.Tasks;
using UnityEngine;
using DuckyNet.Client.Core.Helpers;


namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 网络生命周期管理器 - 统一管理网络连接、房间加入/离开等生命周期事件
    /// </summary>
    public class NetworkLifecycleManager : IDisposable
    {
        private readonly GameContext _context;

        /// <summary>
        /// 连接成功事件
        /// </summary>
        public event Action? OnConnected;

        /// <summary>
        /// 断开连接事件
        /// </summary>
        public event Action<string>? OnDisconnected;

        /// <summary>
        /// 加入房间事件
        /// </summary>
        public event Action? OnJoinedRoom;

        /// <summary>
        /// 离开房间事件
        /// </summary>
        public event Action? OnLeftRoom;

        public NetworkLifecycleManager(GameContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 处理连接成功
        /// </summary>
        public async Task HandleConnectedAsync()
        {
            try
            {
                Debug.Log("[NetworkLifecycleManager] 处理连接成功事件");

                // 检查是否已经有本地角色，如果有则自动上传外观
                await TryUploadExistingCharacterAsync();

                // 如果已经在地图中，需要通知服务器当前场景并获取场景内玩家
                var sceneManager = _context.SceneManager;
                var currentMapName = sceneManager.GetCurrentMapName();

                if (!string.IsNullOrEmpty(currentMapName))
                {
                    Debug.Log($"[NetworkLifecycleManager] 连接时已在地图中，同步场景状态: {currentMapName}");
                    // 延迟一小段时间，确保服务器已完全处理连接
                    await Task.Delay(500);

                    // 通知服务器进入场景
                    await sceneManager.InitializeRoomDataAsync();
                }

                // 通过事件请求启动同步
                if (GameContext.IsInitialized)
                {
                    _context.EventBus.Publish(SyncStartRequestEvent.Instance);
                }
                else
                {
                    // 如果 GameContext 还未初始化，直接调用（向后兼容）
                    StartSyncIfNeeded();
                }

                // 发布 EventBus 事件
                if (GameContext.IsInitialized)
                {
                    _context.EventBus.Publish(NetworkConnectedEvent.Instance);
                }

                // 保持向后兼容：同时触发原有事件
                OnConnected?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLifecycleManager] 处理连接事件失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 处理断开连接
        /// </summary>
        public void HandleDisconnected(string reason)
        {
            try
            {
                Debug.LogWarning($"[NetworkLifecycleManager] 与服务器断开连接: {reason}");

                // 通过事件发布操作，而不是直接调用管理器方法
                _context.EventBus.Publish(SyncStopRequestEvent.Instance);
                
                // 清理场景数据（内部方法，暂时保留直接调用）
                _context.SceneManager.OnLeftRoom();

                // 通知UI（可以通过事件，但暂时保留直接调用，UI响应可能更及时）
                var chatWindow = _context.UIManager.GetWindow<UI.ChatWindow>("Chat");
                chatWindow?.AddSystemMessage($"与服务器断开连接: {reason}", Shared.Services.MessageType.Warning);

                // 发布 EventBus 事件
                if (GameContext.IsInitialized)
                {
                    _context.EventBus.Publish(new NetworkDisconnectedEvent(reason));
                }

                // 保持向后兼容：同时触发原有事件
                OnDisconnected?.Invoke(reason);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLifecycleManager] 处理断开连接事件失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 处理加入房间
        /// </summary>
        public async Task HandleJoinedRoomAsync()
        {
            try
            {
                Debug.Log("[NetworkLifecycleManager] 处理加入房间事件");

                // 初始化场景数据
                await _context.SceneManager.InitializeRoomDataAsync();

                // 通过事件请求启动同步
                if (GameContext.IsInitialized)
                {
                    _context.EventBus.Publish(SyncStartRequestEvent.Instance);
                    _context.EventBus.Publish(RoomJoinedEvent.Instance);
                }
                else
                {
                    StartSyncIfNeeded();
                }

                // 保持向后兼容：同时触发原有事件
                OnJoinedRoom?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLifecycleManager] 处理加入房间事件失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 处理离开房间
        /// </summary>
        public void HandleLeftRoom()
        {
            try
            {
                Debug.Log("[NetworkLifecycleManager] 处理离开房间事件");

                // 通过事件发布操作
                if (GameContext.IsInitialized)
                {
                    _context.EventBus.Publish(SyncStopRequestEvent.Instance);
                }
                else
                {
                    _context.SyncManager?.StopSync();
                }

                // 清理场景数据（内部方法，暂时保留直接调用）
                _context.SceneManager.OnLeftRoom();

                // 发布 EventBus 事件
                if (GameContext.IsInitialized)
                {
                    _context.EventBus.Publish(RoomLeftEvent.Instance);
                }

                // 保持向后兼容：同时触发原有事件
                OnLeftRoom?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLifecycleManager] 处理离开房间事件失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 尝试上传已存在的角色外观（用于先创建角色后连接服务器的场景）
        /// </summary>
        private async Task TryUploadExistingCharacterAsync()
        {
            try
            {
                var customizationManager = _context.CharacterCustomizationManager;
                var localCharacter = customizationManager.GetLocalPlayerCharacter();

                if (localCharacter != null)
                {
                    // 延迟一小段时间，确保连接完全建立
                    await Task.Delay(500);

                    bool success = await Helpers.CharacterAppearanceHelper.UploadCurrentAppearanceAsync();
                    if (success)
                    {
                        Debug.Log("[NetworkLifecycleManager] ✅ 已有角色外观上传成功");
                    }
                }

                // 检查是否已有角色，如果有则立即启动，否则也会启动但等待角色创建
                var hasCharacter = localCharacter != null;

                // 通过事件请求启动同步
                if (GameContext.IsInitialized)
                {
                    _context.EventBus.Publish(SyncStartRequestEvent.Instance);
                    
                    if (hasCharacter)
                    {
                        Debug.Log("[NetworkLifecycleManager] ✅ 角色同步启动请求已发布（已有角色）");
                    }
                    else
                    {
                        Debug.Log("[NetworkLifecycleManager] ✅ 角色同步启动请求已发布（等待角色创建）");
                    }
                }
                else
                {
                    // 向后兼容：直接调用
                    var syncManager = _context.SyncManager;
                    if (syncManager != null)
                    {
                        syncManager.StartSync();
                        Debug.Log("[NetworkLifecycleManager] ✅ 角色同步已启动（向后兼容模式）");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLifecycleManager] 上传已有角色外观失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 启动同步（如果需要）
        /// </summary>
        private void StartSyncIfNeeded()
        {
            var syncManager = _context.SyncManager;
            if (syncManager != null && !syncManager.IsEnabled)
            {
                syncManager.StartSync();
            }
        }

        public void Dispose()
        {
            OnConnected = null;
            OnDisconnected = null;
            OnJoinedRoom = null;
            OnLeftRoom = null;
        }
    }
}


