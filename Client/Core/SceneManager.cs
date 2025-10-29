using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckyNet.Shared.Services;
using DuckyNet.Client.RPC;
using HarmonyLib;

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

        private Type? _levelManagerType;
        private bool _typesInitialized = false;

        public SceneManager()
        {
            _playerModelManager = new PlayerModelManager();
            InitializeTypes();
            RegisterSceneCallbacks();
        }

        /// <summary>
        /// 初始化游戏类型引用
        /// </summary>
        private void InitializeTypes()
        {
            try
            {
                _levelManagerType = AccessTools.TypeByName("LevelManager");
                _typesInitialized = _levelManagerType != null;

                if (_typesInitialized)
                {
                    Debug.Log("[SceneManager] 游戏类型初始化成功");
                }
                else
                {
                    Debug.LogWarning("[SceneManager] LevelManager类型未找到");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneManager] 类型初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册场景回调（通过LevelManager事件监听）
        /// </summary>
        private void RegisterSceneCallbacks()
        {
            try
            {
                // 获取 LevelManager 单例
                var levelManagerType = AccessTools.TypeByName("LevelManager");
                if (levelManagerType == null)
                {
                    Debug.LogWarning("[SceneManager] LevelManager 类型未找到");
                    return;
                }

                var instanceProp = AccessTools.Property(levelManagerType, "Instance");
                var levelManager = instanceProp?.GetValue(null);
                if (levelManager == null)
                {
                    Debug.LogWarning("[SceneManager] LevelManager 实例未初始化");
                    return;
                }

                // 订阅关卡开始初始化事件（离开旧场景）
                var beginInitEvent = AccessTools.Field(levelManagerType, "OnLevelBeginInitializing");
                if (beginInitEvent != null)
                {
                    var eventDelegate = Delegate.CreateDelegate(
                        beginInitEvent.FieldType,
                        this,
                        typeof(SceneManager).GetMethod(nameof(OnLevelBeginInitializing), 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    );
                    var currentDelegate = beginInitEvent.GetValue(levelManager) as Delegate;
                    var combined = Delegate.Combine(currentDelegate, eventDelegate);
                    beginInitEvent.SetValue(levelManager, combined);
                    Debug.Log("[SceneManager] 已订阅 OnLevelBeginInitializing 事件");
                }

                // 订阅关卡初始化完成事件（进入新场景）
                var initEvent = AccessTools.Field(levelManagerType, "OnLevelInitialized");
                if (initEvent != null)
                {
                    var eventDelegate = Delegate.CreateDelegate(
                        initEvent.FieldType,
                        this,
                        typeof(SceneManager).GetMethod(nameof(OnLevelInitialized),
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    );
                    var currentDelegate = initEvent.GetValue(levelManager) as Delegate;
                    var combined = Delegate.Combine(currentDelegate, eventDelegate);
                    initEvent.SetValue(levelManager, combined);
                    Debug.Log("[SceneManager] 已订阅 OnLevelInitialized 事件");
                }

                Debug.Log("[SceneManager] 场景回调注册成功");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneManager] 注册场景回调失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 关卡开始初始化事件处理（离开旧场景）
        /// </summary>
        private void OnLevelBeginInitializing()
        {
            try
            {
                if (string.IsNullOrEmpty(CurrentScene)) return;

                Debug.Log($"[SceneManager] 关卡开始初始化，离开场景: {CurrentScene}");
                NotifySceneUnloading();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneManager] OnLevelBeginInitializing 处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 关卡初始化完成事件处理（进入新场景）
        /// </summary>
        private void OnLevelInitialized()
        {
            try
            {
                var sceneName = GetCurrentLevelInfo();
                if (string.IsNullOrEmpty(sceneName)) return;

                Debug.Log($"[SceneManager] 关卡初始化完成: {sceneName}");
                NotifySceneLoaded(sceneName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneManager] OnLevelInitialized 处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前关卡信息（只返回场景名称）
        /// </summary>
        public string? GetCurrentLevelInfo()
        {
            if (!_typesInitialized) return null;

            try
            {
                var instanceProp = AccessTools.Property(_levelManagerType, "Instance");
                object? levelManager = instanceProp?.GetValue(null);
                if (levelManager == null) return null;

                var getLevelInfoMethod = AccessTools.Method(_levelManagerType, "GetCurrentLevelInfo");
                if (getLevelInfoMethod == null) return null;

                object? levelInfo = getLevelInfoMethod.Invoke(levelManager, null);
                if (levelInfo == null) return null;

                var levelInfoType = levelInfo.GetType();
                var sceneNameField = AccessTools.Field(levelInfoType, "sceneName");

                // 安全地获取字符串字段
                string sceneName = string.Empty;
                if (sceneNameField != null)
                {
                    var sceneNameValue = sceneNameField.GetValue(levelInfo);
                    sceneName = sceneNameValue as string ?? sceneNameValue?.ToString() ?? string.Empty;
                }

                return string.IsNullOrEmpty(sceneName) ? null : sceneName;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneManager] 获取关卡信息失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取当前地图名称
        /// </summary>
        public string? GetCurrentMapName()
        {
            return GetCurrentLevelInfo();
        }

        /// <summary>
        /// 通知场景加载完成
        /// </summary>
        public void NotifySceneLoaded(string sceneName)
        {
            Debug.Log($"[SceneManager] 场景加载完成: {sceneName}");
            CurrentScene = sceneName;
            IsChangingScene = false;

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
        /// 通知场景卸载前（游戏调用）
        /// </summary>
        public void NotifySceneUnloading()
        {
            if (string.IsNullOrEmpty(CurrentScene)) return;

            Debug.Log($"[SceneManager] 场景卸载: {CurrentScene}");
            IsChangingScene = true;

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
                    Debug.Log($"[SceneManager] 已通知服务器进入场景: {sceneName}");
                    
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
                    Debug.LogWarning($"[SceneManager] 进入场景失败: {sceneName}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneManager] 通知服务器进入场景失败: {ex.Message}");
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
                Debug.Log("[SceneManager] 已通知服务器离开场景");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneManager] 通知服务器离开场景失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新玩家场景信息（全局维护）
        /// </summary>
        public void UpdatePlayerScene(PlayerSceneInfo playerSceneInfo)
        {
            _playerScenes[playerSceneInfo.SteamId] = playerSceneInfo;
            Debug.Log($"[SceneManager] 更新玩家场景: {playerSceneInfo.SteamId} ({playerSceneInfo.PlayerInfo?.SteamName ?? "Unknown"}) -> {playerSceneInfo.SceneName} (HasCharacter: {playerSceneInfo.HasCharacter}, CurrentScene: {CurrentScene})");

            // 触发事件
            OnPlayerEnteredScene?.Invoke(playerSceneInfo);

            // 如果是当前场景的玩家，且没有模型，则创建
            if (playerSceneInfo.SceneName == CurrentScene)
            {
                if (!playerSceneInfo.HasCharacter)
                {
                    Debug.Log($"[SceneManager] 玩家 {playerSceneInfo.SteamId} 尚未创建角色，等待角色创建完成后再创建模型");
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
                        Debug.Log($"[SceneManager] 玩家 {playerSceneInfo.SteamId} 还没有角色，等待角色创建完成");
                        // TODO: 可以订阅角色创建事件，当角色创建后自动创建模型
                    }
                }
                else
                {
                    Debug.Log($"[SceneManager] 玩家 {playerSceneInfo.SteamId} 的模型已存在，跳过创建");
                }
            }
            else
            {
                Debug.Log($"[SceneManager] 玩家 {playerSceneInfo.SteamId} 不在当前场景 ({CurrentScene ?? "null"})，跳过模型创建");
            }
        }

        /// <summary>
        /// 移除玩家场景信息
        /// </summary>
        public void RemovePlayerScene(string steamId, string sceneName)
        {
            if (_playerScenes.Remove(steamId))
            {
                Debug.Log($"[SceneManager] 移除玩家场景: {steamId} <- {sceneName}");
                
                // 触发事件
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
                Debug.Log($"[SceneManager] 更新玩家外观: {steamId} ({appearanceData?.Length ?? 0} bytes)");

                // 如果玩家模型不存在，但玩家在当前场景中，则创建模型
                if (!_playerModelManager.HasPlayerModel(steamId))
                {
                    // 检查玩家是否在当前场景中
                    if (_playerScenes.TryGetValue(steamId, out var playerSceneInfo))
                    {
                        if (playerSceneInfo.SceneName == CurrentScene)
                        {
                            Debug.Log($"[SceneManager] 收到外观数据但模型不存在，尝试创建模型: {steamId}");
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
                    
                    Debug.Log($"[SceneManager] 玩家不在当前场景，跳过外观更新: {steamId}");
                    return;
                }

                // 应用外观到现有模型
                _playerModelManager.UpdatePlayerAppearance(steamId, appearanceData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneManager] 更新玩家外观失败: {ex.Message}");
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
                    Debug.LogWarning($"[SceneManager] 延迟应用外观失败: 外观数据为空 - {steamId}");
                    return;
                }

                // 等待一小段时间确保模型已创建
                await System.Threading.Tasks.Task.Delay(100);
                
                // 再次尝试应用外观
                if (_playerModelManager.HasPlayerModel(steamId))
                {
                    _playerModelManager.UpdatePlayerAppearance(steamId, appearanceData);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneManager] 延迟应用外观失败: {ex.Message}");
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
            Debug.Log($"[SceneManager] 为当前场景创建模型，玩家数: {playersInScene.Count}");

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
            Debug.Log("[SceneManager] 离开房间，清理所有数据");
            
            ClearSceneModels();
            _playerScenes.Clear();
            CurrentScene = null;
            IsChangingScene = false;
        }

        /// <summary>
        /// 初始化房间数据（进入房间后调用）
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
                
                Debug.Log($"[SceneManager] 初始化房间数据，玩家数: {allPlayerScenes.Length}");
                
                _playerScenes.Clear();
                foreach (var playerScene in allPlayerScenes)
                {
                    _playerScenes[playerScene.SteamId] = playerScene;
                }

                // 获取当前地图名称
                var currentMapName = GetCurrentMapName();
                if (!string.IsNullOrEmpty(currentMapName))
                {
                    // 如果场景变化了，需要通知服务器
                    if (CurrentScene != currentMapName)
                    {
                        Debug.Log($"[SceneManager] 检测到场景变化，通知服务器: {CurrentScene ?? "null"} -> {currentMapName}");
                        await NotifyServerEnterSceneAsync(currentMapName);
                    }
                    else
                    {
                        CurrentScene = currentMapName;
                        Debug.Log($"[SceneManager] 当前地图: {currentMapName}");
                        
                        // 为当前场景创建模型
                        CreateModelsForCurrentScene();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneManager] 初始化房间数据失败: {ex.Message}");
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
            }
        }

        public void Dispose()
        {
            OnLeftRoom();
            _playerModelManager?.Dispose();
            OnSceneLoaded = null;
            OnSceneUnloading = null;
            OnPlayerEnteredScene = null;
            OnPlayerLeftScene = null;
        }
    }
}

