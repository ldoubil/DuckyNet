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
                _eventSubscriber.EnsureInitializedAndSubscribe();
                _eventSubscriber.Subscribe<PlayerEnteredSceneEvent>(OnPlayerEnteredSceneEvent);
            }
        }


        /// <summary>
        /// 处理玩家进入场景事件（通过 EventBus）
        /// </summary>
        private void OnPlayerEnteredSceneEvent(PlayerEnteredSceneEvent evt)
        {

        }

        /// <summary>
        /// 玩家进入场景通知（服务器调用）
        /// </summary>
        public void OnPlayerEnteredScene(PlayerSceneInfo playerSceneInfo)
        {
            try
            {
                Debug.Log($"[SceneClientService] 玩家进入场景: {playerSceneInfo.PlayerInfo?.SteamName ?? playerSceneInfo.SteamId} -> {playerSceneInfo.SceneName} ");
                if (GameContext.IsInitialized)
                {
                    _eventSubscriber.EventBus.Publish(new PlayerEnteredSceneEvent(playerSceneInfo));
                }
            }catch(Exception ex)
            {
                Debug.LogError($"[SceneClientService] 处理玩家进入场景失败: {ex.Message}");
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

                if (GameContext.IsInitialized)
                {
                    _eventSubscriber.EventBus.Publish(new PlayerLeftSceneEvent(steamId, sceneName));
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

