using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckyNet.Shared.Services;
using DuckyNet.Client.RPC;
using DuckyNet.Client.Core.Helpers;


namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 场景管理器 - 管理玩家在场景中的分布和模型创建
    /// </summary>
    public class SceneManager : IDisposable
    {
        /// <summary>
        /// 当前场景名称（地图名称）
        /// </summary>
        public string? CurrentScene { get; private set; }

        /// <summary>
        /// 全局玩家场景映射（steamId -> PlayerSceneInfo）
        /// </summary>
        private readonly Dictionary<string, PlayerSceneInfo> _playerScenes = new Dictionary<string, PlayerSceneInfo>();

        /// <summary>
        /// 玩家模型管理器
        /// </summary>
        private readonly PlayerModelManager _playerModelManager;

        /// <summary>
        /// 事件订阅助手
        /// </summary>
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();

        /// <summary>
        /// 是否正在切换场景
        /// </summary>
        public bool IsChangingScene { get; private set; }

        /// <summary>
        /// 场景切换完成事件
        /// </summary>
        public event Action<string>? OnSceneLoaded;

        /// <summary>
        /// 场景卸载前事件
        /// </summary>
        public event Action<string>? OnSceneUnloading;

        /// <summary>
        /// 玩家进入场景事件（任何玩家进入任何场景）
        /// </summary>
        public event Action<PlayerSceneInfo>? OnPlayerEnteredScene;

        /// <summary>
        /// 玩家离开场景事件（任何玩家离开任何场景）
        /// </summary>
        public event Action<string, string>? OnPlayerLeftScene; // steamId, sceneName

        public SceneManager()
        {
            _playerModelManager = new PlayerModelManager();
            
            // 订阅外观更新事件和场景事件
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
            _eventSubscriber.EnsureInitializedAndSubscribe();
            
            // 订阅玩家外观更新事件
            _eventSubscriber.Subscribe<PlayerAppearanceUpdatedEvent>(OnPlayerAppearanceUpdated);
            
            // 订阅场景加载/卸载事件（来自 SceneListener）
            _eventSubscriber.Subscribe<SceneLoadedEvent>(OnSceneLoadedEvent);
            _eventSubscriber.Subscribe<SceneUnloadingEvent>(OnSceneUnloadingEvent);
        }

        /// <summary>
        /// 处理玩家外观更新事件
        /// </summary>
        private void OnPlayerAppearanceUpdated(PlayerAppearanceUpdatedEvent evt)
        {
            UpdatePlayerAppearance(evt.SteamId, evt.AppearanceData);
        }

        /// <summary>
        /// 处理场景加载事件（来自 SceneListener）
        /// </summary>
        private void OnSceneLoadedEvent(SceneLoadedEvent evt)
        {
            NotifySceneLoaded(evt.SceneName);
        }

        /// <summary>
        /// 处理场景卸载事件（来自 SceneListener）
        /// </summary>
        private void OnSceneUnloadingEvent(SceneUnloadingEvent evt)
        {
            if (!string.IsNullOrEmpty(CurrentScene))
            {
                NotifySceneUnloading();
            }
        }

        /// <summary>
        /// 获取当前关卡信息（通过 SceneInfoProvider/SceneListener）
        /// </summary>
        public string? GetCurrentLevelInfo()
        {
            return SceneInfoProvider.GetCurrentLevelInfo();
        }

        /// <summary>
        /// 获取当前地图名称（工具方法，通过 SceneInfoProvider/SceneListener）
        /// </summary>
        public string? GetCurrentMapName()
        {
            return SceneInfoProvider.GetCurrentMapName();
        }

        /// <summary>
        /// 通知场景加载完成（由 SceneListener 通过事件触发）
        /// </summary>
        public void NotifySceneLoaded(string sceneName)
        {
            UnityEngine.Debug.Log($"[SceneManager] 场景加载完成: {sceneName}");
            
            // 如果场景已经变化，先清理旧场景
            if (!string.IsNullOrEmpty(CurrentScene) && CurrentScene != sceneName)
            {
                UnityEngine.Debug.Log($"[SceneManager] 场景变化: {CurrentScene} -> {sceneName}");
                // 清理旧场景的模型
                ClearSceneModels();
            }
            
            CurrentScene = sceneName;
            IsChangingScene = false;

            // 注意：SceneListener 已经发布了 SceneLoadedEvent 和 SceneNameUpdatedEvent
            // 这里保持向后兼容：同时触发原有事件
            OnSceneLoaded?.Invoke(sceneName);

            // 通知服务器进入场景
            if (GameContext.IsInitialized && GameContext.Instance.RpcClient.IsConnected)
            {
                _ = NotifyServerEnterSceneAsync(sceneName);
            }

            // 为当前场景的玩家创建模型
            CreateModelsForCurrentScene();
        }

        /// <summary>
        /// 通知场景卸载前（由 SceneListener 通过事件触发）
        /// </summary>
        public void NotifySceneUnloading()
        {
            if (string.IsNullOrEmpty(CurrentScene)) return;

            UnityEngine.Debug.Log($"[SceneManager] 场景卸载: {CurrentScene}");
            IsChangingScene = true;

            // 注意：SceneListener 已经发布了 SceneUnloadingEvent 和 SceneNameUpdatedEvent
            // 这里保持向后兼容：同时触发原有事件
            OnSceneUnloading?.Invoke(CurrentScene);

            // 通知服务器离开场景
            if (GameContext.IsInitialized && GameContext.Instance.RpcClient.IsConnected)
            {
                _ = NotifyServerLeaveSceneAsync();
            }

            // 清理当前场景的所有模型
            ClearSceneModels();
        }

        /// <summary>
        /// 通知服务器进入场景
        /// </summary>
        private async System.Threading.Tasks.Task NotifyServerEnterSceneAsync(string sceneName)
        {
            try
            {
                var rpcClient = GameContext.Instance.RpcClient;
                var context = new RPC.ClientServerContext(rpcClient);
                var sceneService = new Shared.Services.Generated.SceneServiceClientProxy(context);
                
                var success = await sceneService.EnterSceneAsync(sceneName);
                if (success)
                {
                    UnityEngine.Debug.Log($"[SceneManager] 已通知服务器进入场景: {sceneName}");
                    
                    // 获取场景内的所有玩家
                    var players = await sceneService.GetScenePlayersAsync(sceneName);
                    foreach (var player in players)
                    {
                        UpdatePlayerScene(player);
                    }

                    // 重新创建模型
                    CreateModelsForCurrentScene();
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[SceneManager] 进入场景失败: {sceneName}");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneManager] 通知服务器进入场景失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知服务器离开场景
        /// </summary>
        private async System.Threading.Tasks.Task NotifyServerLeaveSceneAsync()
        {
            try
            {
                var rpcClient = GameContext.Instance.RpcClient;
                var context = new RPC.ClientServerContext(rpcClient);
                var sceneService = new Shared.Services.Generated.SceneServiceClientProxy(context);
                
                await sceneService.LeaveSceneAsync();
                UnityEngine.Debug.Log("[SceneManager] 已通知服务器离开场景");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneManager] 通知服务器离开场景失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新玩家场景信息（全局维护）
        /// </summary>
        public void UpdatePlayerScene(PlayerSceneInfo playerSceneInfo)
        {
            _playerScenes[playerSceneInfo.SteamId] = playerSceneInfo;
            UnityEngine.Debug.Log($"[SceneManager] 更新玩家场景: {playerSceneInfo.SteamId} ({playerSceneInfo.PlayerInfo?.SteamName ?? "Unknown"}) -> {playerSceneInfo.SceneName} (HasCharacter: {playerSceneInfo.HasCharacter}, CurrentScene: {CurrentScene})");

            // 发布 EventBus 事件
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new PlayerEnteredSceneEvent(playerSceneInfo));
            }

            // 保持向后兼容：同时触发原有事件
            OnPlayerEnteredScene?.Invoke(playerSceneInfo);

            // 如果是当前场景的玩家，且没有模型，则创建
            if (playerSceneInfo.SceneName == CurrentScene)
            {
                if (!playerSceneInfo.HasCharacter)
                {
                    UnityEngine.Debug.Log($"[SceneManager] 玩家 {playerSceneInfo.SteamId} 尚未创建角色，等待角色创建完成后再创建模型");
                    // 即使没有角色，也触发创建流程，因为可能需要等待角色创建
                }
                
                if (!_playerModelManager.HasPlayerModel(playerSceneInfo.SteamId))
                {
                    if (playerSceneInfo.HasCharacter)
                    {
                        _playerModelManager.CreatePlayerModel(playerSceneInfo);
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"[SceneManager] 玩家 {playerSceneInfo.SteamId} 还没有角色，等待角色创建完成");
                        // TODO: 可以订阅角色创建事件，当角色创建后自动创建模型
                    }
                }
                else
                {
                    UnityEngine.Debug.Log($"[SceneManager] 玩家 {playerSceneInfo.SteamId} 的模型已存在，跳过创建");
                }
            }
            else
            {
                UnityEngine.Debug.Log($"[SceneManager] 玩家 {playerSceneInfo.SteamId} 不在当前场景 ({CurrentScene ?? "null"})，跳过模型创建");
            }
        }

        /// <summary>
        /// 移除玩家场景信息
        /// </summary>
        public void RemovePlayerScene(string steamId, string sceneName)
        {
            if (_playerScenes.Remove(steamId))
            {
                UnityEngine.Debug.Log($"[SceneManager] 移除玩家场景: {steamId} <- {sceneName}");
                
                // 发布 EventBus 事件
                if (GameContext.IsInitialized)
                {
                    GameContext.Instance.EventBus.Publish(new PlayerLeftSceneEvent(steamId, sceneName));
                }

                // 保持向后兼容：同时触发原有事件
                OnPlayerLeftScene?.Invoke(steamId, sceneName);

                // 如果有模型，销毁它
                _playerModelManager.DestroyPlayerModel(steamId);
            }
        }

        /// <summary>
        /// 更新玩家外观
        /// </summary>
        public void UpdatePlayerAppearance(string steamId, byte[] appearanceData)
        {
            try
            {
                UnityEngine.Debug.Log($"[SceneManager] 更新玩家外观: {steamId} ({appearanceData?.Length ?? 0} bytes)");

                // 如果玩家模型不存在，但玩家在当前场景中，则创建模型
                if (!_playerModelManager.HasPlayerModel(steamId))
                {
                    // 检查玩家是否在当前场景中
                    if (_playerScenes.TryGetValue(steamId, out var playerSceneInfo))
                    {
                        if (playerSceneInfo.SceneName == CurrentScene)
                        {
                            UnityEngine.Debug.Log($"[SceneManager] 收到外观数据但模型不存在，尝试创建模型: {steamId}");
                            // 更新 PlayerSceneInfo 的 HasCharacter 状态
                            playerSceneInfo.HasCharacter = true;
                            _playerScenes[steamId] = playerSceneInfo;
                            
                            // 创建模型
                            if (_playerModelManager.CreatePlayerModel(playerSceneInfo))
                            {
                                // 等待一小段时间后应用外观（确保模型已创建）
                                ApplyAppearanceAfterModelCreated(steamId, appearanceData);
                            }
                            return;
                        }
                    }
                    
                    UnityEngine.Debug.Log($"[SceneManager] 玩家不在当前场景，跳过外观更新: {steamId}");
                    return;
                }

                // 应用外观到现有模型
                if (appearanceData != null)
                {
                    _playerModelManager.UpdatePlayerAppearance(steamId, appearanceData);
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[SceneManager] 外观数据为 null，跳过更新: {steamId}");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneManager] 更新玩家外观失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 在模型创建后应用外观（延迟应用）
        /// </summary>
        private async void ApplyAppearanceAfterModelCreated(string steamId, byte[]? appearanceData)
        {
            try
            {
                if (appearanceData == null || appearanceData.Length == 0)
                {
                    UnityEngine.Debug.LogWarning($"[SceneManager] 延迟应用外观失败: 外观数据为空 - {steamId}");
                    return;
                }

                // 等待一小段时间确保模型已创建
                await System.Threading.Tasks.Task.Delay(100);
                
                // 再次尝试应用外观
                if (_playerModelManager.HasPlayerModel(steamId) && appearanceData != null)
                {
                    _playerModelManager.UpdatePlayerAppearance(steamId, appearanceData);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneManager] 延迟应用外观失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定场景的玩家列表
        /// </summary>
        public List<PlayerSceneInfo> GetPlayersInScene(string sceneName)
        {
            return _playerScenes.Values
                .Where(p => p.SceneName == sceneName)
                .ToList();
        }

        /// <summary>
        /// 获取所有玩家场景信息
        /// </summary>
        public IReadOnlyDictionary<string, PlayerSceneInfo> GetAllPlayerScenes()
        {
            return _playerScenes;
        }

        /// <summary>
        /// 为当前场景的所有玩家创建模型
        /// </summary>
        private void CreateModelsForCurrentScene()
        {
            if (string.IsNullOrEmpty(CurrentScene)) return;
            if (!GameContext.IsInitialized) return;

            var playersInScene = GetPlayersInScene(CurrentScene);
            UnityEngine.Debug.Log($"[SceneManager] 为当前场景创建模型，玩家数: {playersInScene.Count}");

            foreach (var playerInfo in playersInScene)
            {
                if (playerInfo.HasCharacter && !_playerModelManager.HasPlayerModel(playerInfo.SteamId))
                {
                    _playerModelManager.CreatePlayerModel(playerInfo);
                }
            }
        }

        /// <summary>
        /// 清理当前场景的所有模型
        /// </summary>
        private void ClearSceneModels()
        {
            _playerModelManager.ClearAllModels();
        }

        /// <summary>
        /// 离开房间时清理所有数据
        /// </summary>
        public void OnLeftRoom()
        {
            UnityEngine.Debug.Log("[SceneManager] 离开房间，清理所有数据");
            
            ClearSceneModels();
            _playerScenes.Clear();
            CurrentScene = null;
            IsChangingScene = false;
        }


        /// <summary>
        /// 初始化房间数据（进入房间后调用）
        /// 在启用 mod 时调用一次，获取当前场景并初始化
        /// </summary>
        public async System.Threading.Tasks.Task InitializeRoomDataAsync()
        {
            if (!GameContext.IsInitialized || !GameContext.Instance.RpcClient.IsConnected) return;

            try
            {
                var rpcClient = GameContext.Instance.RpcClient;
                var context = new RPC.ClientServerContext(rpcClient);
                var sceneService = new Shared.Services.Generated.SceneServiceClientProxy(context);
                
                // 获取所有玩家的场景信息
                var allPlayerScenes = await sceneService.GetAllPlayerScenesAsync();
                
                UnityEngine.Debug.Log($"[SceneManager] 初始化房间数据，玩家数: {allPlayerScenes.Length}");
                
                _playerScenes.Clear();
                foreach (var playerScene in allPlayerScenes)
                {
                    _playerScenes[playerScene.SteamId] = playerScene;
                }

                // 初始化时获取一次当前场景名称（通过 SceneInfoProvider/SceneListener）
                var currentMapName = GetCurrentMapName();
                if (!string.IsNullOrEmpty(currentMapName))
                {
                    // 如果当前场景为空，说明是第一次初始化，设置场景并通知服务器
                    if (string.IsNullOrEmpty(CurrentScene))
                    {
                        CurrentScene = currentMapName;
                        UnityEngine.Debug.Log($"[SceneManager] 初始化时检测到场景: {currentMapName}，通知服务器");
                        
                        // 发布场景名称更新事件（用于 SyncManager 等更新缓存）
                        var eventBus = GameContext.Instance.EventBus;
                        eventBus.Publish(new SceneNameUpdatedEvent(currentMapName, isInScene: true));
                        
                        // 通知服务器进入场景
                        await NotifyServerEnterSceneAsync(currentMapName);
                        
                        // 为当前场景的玩家创建模型
                        CreateModelsForCurrentScene();
                    }
                    else if (CurrentScene != currentMapName)
                    {
                        // 如果场景已经变化（理论上不应该发生，因为应该通过事件处理）
                        UnityEngine.Debug.LogWarning($"[SceneManager] 初始化时检测到场景变化: {CurrentScene} -> {currentMapName}");
                    }
                }
                else
                {
                    UnityEngine.Debug.Log("[SceneManager] 初始化时未检测到场景（可能不在场景中）");
                    
                    // 即使没有场景，也发布事件通知其他系统
                    var eventBus = GameContext.Instance.EventBus;
                    eventBus.Publish(new SceneNameUpdatedEvent(string.Empty, isInScene: false));
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SceneManager] 初始化房间数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化玩家模型管理器（在GameContext初始化后调用）
        /// </summary>
        public void InitializePlayerModelManager()
        {
            if (GameContext.IsInitialized)
            {
                _playerModelManager.SetUnitManager(GameContext.Instance.UnitManager);
                _playerModelManager.SetLocalPlayerSteamId(GameContext.Instance.LocalPlayer.Info.SteamId);
                
                // 确保订阅事件（如果之前未订阅）
                SubscribeToEvents();
            }
        }

        public void Dispose()
        {
            _eventSubscriber?.Dispose();
            OnLeftRoom();
            _playerModelManager?.Dispose();
            OnSceneLoaded = null;
            OnSceneUnloading = null;
            OnPlayerEnteredScene = null;
            OnPlayerLeftScene = null;
        }
    }
}

