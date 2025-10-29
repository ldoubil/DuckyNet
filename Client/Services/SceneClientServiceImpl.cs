using System;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.Helpers;
using UnityEngine;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 场景客户端服务实现
    /// </summary>
    public class SceneClientServiceImpl : ISceneClientService, IDisposable
    {
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();

        public SceneClientServiceImpl()
        {
            // 订阅玩家进入场景事件，自动请求外观数据
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
            
            // 订阅玩家进入场景事件，自动请求外观数据
            _eventSubscriber.Subscribe<PlayerEnteredSceneEvent>(OnPlayerEnteredSceneEvent);
        }

        /// <summary>
        /// 处理玩家进入场景事件（通过 EventBus）
        /// </summary>
        private void OnPlayerEnteredSceneEvent(PlayerEnteredSceneEvent evt)
        {
            // 如果玩家在当前场景且已创建角色，尝试获取外观数据
            if (!GameContext.IsInitialized) return;

            var sceneManager = GameContext.Instance.SceneManager;
            var currentScene = sceneManager.GetCurrentMapName();

            if (evt.PlayerInfo.HasCharacter && 
                evt.PlayerInfo.SceneName == currentScene)
            {
                _ = RequestPlayerAppearanceAsync(evt.PlayerInfo.SteamId);
            }
        }

        /// <summary>
        /// 玩家进入场景通知（服务器调用）
        /// </summary>
        public void OnPlayerEnteredScene(PlayerSceneInfo playerSceneInfo)
        {
            try
            {
                Debug.Log($"[SceneClientService] 玩家进入场景: {playerSceneInfo.PlayerInfo?.SteamName ?? playerSceneInfo.SteamId} -> {playerSceneInfo.SceneName} (HasCharacter: {playerSceneInfo.HasCharacter})");

                // 更新场景管理器（内部会发布 PlayerEnteredSceneEvent）
                if (GameContext.IsInitialized)
                {
                    var sceneManager = GameContext.Instance.SceneManager;
                    sceneManager.UpdatePlayerScene(playerSceneInfo);
                    // 外观数据请求将通过事件订阅处理
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneClientService] 处理玩家进入场景失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 主动请求玩家外观数据
        /// </summary>
        private async System.Threading.Tasks.Task RequestPlayerAppearanceAsync(string steamId)
        {
            try
            {
                if (!GameContext.IsInitialized || !GameContext.Instance.RpcClient.IsConnected)
                {
                    return;
                }

                var localPlayer = GameContext.Instance.LocalPlayer;
                if (steamId == localPlayer.Info.SteamId)
                {
                    return; // 跳过本地玩家
                }

                Debug.Log($"[SceneClientService] 请求玩家外观数据: {steamId}");
                
                var context = new RPC.ClientServerContext(GameContext.Instance.RpcClient);
                var proxy = new Shared.Services.Generated.CharacterServiceClientProxy(context);
                
                var appearanceData = await proxy.GetAppearanceAsync(steamId);
                
                if (appearanceData != null && appearanceData.Length > 0)
                {
                    Debug.Log($"[SceneClientService] 收到玩家外观数据: {steamId} ({appearanceData.Length} bytes)");
                    
                    // 发布外观更新事件（而不是直接调用 SceneManager）
                    if (GameContext.IsInitialized)
                    {
                        GameContext.Instance.EventBus.Publish(new PlayerAppearanceUpdatedEvent(steamId, appearanceData));
                    }
                }
                else
                {
                    Debug.Log($"[SceneClientService] 玩家尚未上传外观数据: {steamId}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneClientService] 请求玩家外观数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 玩家离开场景通知（服务器调用）
        /// </summary>
        public void OnPlayerLeftScene(string steamId, string sceneName)
        {
            try
            {
                Debug.Log($"[SceneClientService] 玩家离开场景: {steamId} <- {sceneName}");

                // 更新场景管理器（内部会发布 PlayerLeftSceneEvent）
                if (GameContext.IsInitialized)
                {
                    var sceneManager = GameContext.Instance.SceneManager;
                    sceneManager.RemovePlayerScene(steamId, sceneName);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneClientService] 处理玩家离开场景失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _eventSubscriber?.Dispose();
        }
    }
}

