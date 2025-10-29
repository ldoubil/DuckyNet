using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using DuckyNet.Client.RPC;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.RPC;
using DuckyNet.Client.Core.Helpers;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 同步管理器 - 负责定期发送本地角色状态到服务器
    /// </summary>
    public class SyncManager : IDisposable
    {
        private readonly RpcClient _rpcClient;
        private readonly CharacterCustomizationManager _customizationManager;
        private readonly LocalPlayer _localPlayer;
        private SceneManager? _sceneManager;

        private IClientContext? _serverContext;
        private CancellationTokenSource? _syncCts;
        private Task? _syncTask;

        // 同步配置
        private float _syncInterval = 0.05f; // 每秒20次
        private bool _isEnabled = false;
        
        // 缓存上次检查的角色对象，避免频繁调用 GetLocalPlayerCharacter
        private GameObject? _cachedCharacter = null;
        private float _cacheValidDuration = 0.2f; // 缓存有效期 200ms
        private float _lastCacheTime = -1f;
        
        // 缓存场景检查结果，减少频繁检查
        private bool _cachedIsInScene = false;
        private string? _cachedSceneName = null; // 缓存场景名称，用于检测场景切换
        private float _sceneCheckInterval = 0.5f; // 场景检查间隔 500ms
        private float _lastSceneCheckTime = -1f;

        public SyncManager(
            RpcClient rpcClient, 
            CharacterCustomizationManager customizationManager,
            LocalPlayer localPlayer,
            SceneManager? sceneManager = null)
        {
            _rpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
            _customizationManager = customizationManager ?? throw new ArgumentNullException(nameof(customizationManager));
            _localPlayer = localPlayer ?? throw new ArgumentNullException(nameof(localPlayer));
            _sceneManager = sceneManager; // 可选，允许在创建时传入

            Debug.Log("[SyncManager] 同步管理器已创建");
        }
        
        /// <summary>
        /// 设置场景管理器（如果创建时未传入）
        /// </summary>
        public void SetSceneManager(SceneManager sceneManager)
        {
            _sceneManager = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));
        }

        /// <summary>
        /// 启动同步
        /// </summary>
        public void StartSync()
        {
            if (_isEnabled)
            {
                Debug.LogWarning("[SyncManager] 同步已在运行");
                return;
            }

            try
            {
                // 创建服务器上下文
                _serverContext = new ClientServerContext(_rpcClient);
                
                // 请求完整场景状态
                _ = RequestFullStateAsync();

                // 启动同步循环
                _syncCts = new CancellationTokenSource();
                _syncTask = SyncLoopAsync(_syncCts.Token);
                _isEnabled = true;

                Debug.Log($"[SyncManager] ✅ 同步已启动（间隔: {_syncInterval:F3}秒）");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SyncManager] 启动同步失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 停止同步
        /// </summary>
        public void StopSync()
        {
            if (!_isEnabled)
            {
                return;
            }

            try
            {
                _syncCts?.Cancel();
                _syncTask?.Wait(1000); // 最多等待1秒
                _isEnabled = false;

                Debug.Log("[SyncManager] 同步已停止");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SyncManager] 停止同步失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 请求完整场景状态
        /// </summary>
        private Task RequestFullStateAsync()
        {
            try
            {
                if (_serverContext == null)
                {
                    Debug.LogWarning("[SyncManager] 服务器上下文未初始化");
                    return Task.CompletedTask;
                }

                Debug.Log("[SyncManager] 请求完整场景状态...");
                _serverContext.Invoke<ICharacterSyncService>("RequestFullState");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SyncManager] 请求完整状态失败: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// 同步循环
        /// </summary>
        private async Task SyncLoopAsync(CancellationToken ct)
        {
            Debug.Log("[SyncManager] 同步循环开始");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_syncInterval), ct);

                    // 发送本地角色状态
                    await SendLocalStateAsync();
                }
                catch (TaskCanceledException)
                {
                    // 正常取消，忽略
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SyncManager] 同步循环错误: {ex.Message}");
                    // 继续运行，不中断循环
                }
            }

            Debug.Log("[SyncManager] 同步循环结束");
        }

        /// <summary>
        /// 检查是否应该同步（两个条件：玩家存在 + 在地图中）
        /// </summary>
        private bool ShouldSync()
        {
            // 条件1: 检查玩家是否存在
            var currentTime = Time.time;
            var character = _cachedCharacter;
            
            // 检查角色缓存是否有效
            bool characterCacheValid = character != null 
                && (currentTime - _lastCacheTime) < _cacheValidDuration
                && character.activeInHierarchy;
            
            if (!characterCacheValid)
            {
                // 缓存无效或过期，重新获取角色
                character = _customizationManager.GetLocalPlayerCharacter();
                _cachedCharacter = character;
                _lastCacheTime = currentTime;
            }
            
            // 玩家不存在，不同步
            if (character == null || !character.activeInHierarchy)
            {
                // 清除无效缓存
                _cachedCharacter = null;
                return false;
            }

            // 条件2: 检查是否在地图中
            // 使用缓存减少场景检查频率，但如果场景变化则立即更新缓存
            bool needSceneCheck = (currentTime - _lastSceneCheckTime) >= _sceneCheckInterval;
            
            // 如果场景管理器有场景变化通知，检查场景名称是否变化
            if (!needSceneCheck && _sceneManager != null)
            {
                var currentSceneName = _sceneManager.GetCurrentMapName();
                if (currentSceneName != _cachedSceneName)
                {
                    // 场景名称变化，强制更新缓存
                    needSceneCheck = true;
                    Debug.Log($"[SyncManager] 检测到场景切换: {_cachedSceneName} -> {currentSceneName}");
                }
            }
            
            if (needSceneCheck)
            {
                bool isInScene = false;
                string? mapName = null;
                
                if (_sceneManager != null)
                {
                    mapName = _sceneManager.GetCurrentMapName();
                    isInScene = !string.IsNullOrEmpty(mapName);
                }
                else if (GameContext.IsInitialized && GameContext.Instance.SceneManager != null)
                {
                    // 如果创建时未传入，尝试从 GameContext 获取
                    mapName = GameContext.Instance.SceneManager.GetCurrentMapName();
                    isInScene = !string.IsNullOrEmpty(mapName);
                }
                
                _cachedIsInScene = isInScene;
                _cachedSceneName = mapName;
                _lastSceneCheckTime = currentTime;
            }
            
            // 不在场景中，不同步
            if (!_cachedIsInScene)
            {
                return false;
            }

            // 两个条件都满足，可以同步
            return true;
        }

        /// <summary>
        /// 发送本地角色状态
        /// </summary>
        private async Task SendLocalStateAsync()
        {
            try
            {
                // 检查连接状态
                if (!_rpcClient.IsConnected)
                {
                    return; // 未连接，静默返回
                }
                
                if (_serverContext == null)
                {
                    return;
                }

                // 先检查同步条件：玩家存在 + 在地图中
                if (!ShouldSync())
                {
                    // 条件不满足，静默返回（不发送任何数据）
                    return;
                }

                // 确保角色对象仍然有效（使用 Unity 的 null 检查）
                // 注意：Unity 中 GameObject 被销毁后不会变成真正的 null，需要检查 activeInHierarchy
                if (_cachedCharacter == null || !_cachedCharacter.activeInHierarchy)
                {
                    _cachedCharacter = null;
                    return;
                }
                
                // 创建同步数据
                var syncData = Helpers.CharacterSyncHelper.FromUnity(_localPlayer.Info.SteamId, _cachedCharacter);
                
                // 验证同步数据的有效性
                if (string.IsNullOrEmpty(syncData.PlayerId))
                {
                    Debug.LogWarning("[SyncManager] PlayerId 为空，跳过同步");
                    return;
                }
                
                // 检测 Attack Trigger 变化（需要通过其他方式检测，这里先用计数）
                // 注意：Animator Trigger 无法直接检测，需要通过游戏事件或其他方式
                // 暂时保持 AttackTriggerCount 为 0，后续可以通过拦截攻击方法来递增
                
                // 发送到服务器
                _serverContext.Invoke<ICharacterSyncService>("SyncCharacterState", syncData);
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // 减少日志噪音，只在关键错误时输出
                if (!(ex is TaskCanceledException))
                {
                    Debug.LogWarning($"[SyncManager] 发送状态失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 手动触发一次同步（立即）
        /// </summary>
        public async Task SyncNow()
        {
            try
            {
                await SendLocalStateAsync();
                Debug.Log("[SyncManager] 手动同步完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SyncManager] 手动同步失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置同步间隔（秒）
        /// </summary>
        public void SetSyncInterval(float interval)
        {
            if (interval <= 0)
            {
                Debug.LogWarning($"[SyncManager] 无效的同步间隔: {interval}，使用默认值 0.05");
                interval = 0.05f;
            }
            
            _syncInterval = interval;
            
            // 如果正在运行，重启同步循环以应用新间隔
            if (_isEnabled)
            {
                StopSync();
                StartSync();
                Debug.Log($"[SyncManager] 同步间隔已更新: {interval:F3}秒");
            }
            else
            {
                Debug.Log($"[SyncManager] 同步间隔已设置: {interval:F3}秒（将在启动时应用）");
            }
        }

        public bool IsEnabled => _isEnabled;

        public void Dispose()
        {
            StopSync();
            _syncCts?.Dispose();
        }
    }
}

