using System;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.EventBus;
using DuckyNet.Client.Core.EventBus.Events;
using UnityEngine;
using DuckyNet.Shared.Data;

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
        public void OnPlayerEnteredScene(PlayerInfo playerInfo, ScenelData scenelData)
        {
            try
            {
                Debug.Log($"[SceneClientService] 玩家进入场景: {playerInfo.SteamName} -> {scenelData.SceneName} ");
                if (GameContext.IsInitialized)
                {
                    _eventSubscriber.EventBus.Publish(new PlayerEnteredSceneEvent(playerInfo, scenelData));
                }
            }catch(Exception ex)
            {
                Debug.LogError($"[SceneClientService] 处理玩家进入场景失败: {ex.Message}");
            }
        }
        
     

        /// <summary>
        /// 玩家离开场景通知（服务器调用）
        /// </summary>
        public void OnPlayerLeftScene(PlayerInfo playerInfo, ScenelData scenelData)
        {
            try
            {
                Debug.Log($"[SceneClientService] 玩家离开场景: {playerInfo.SteamName} <- {scenelData.SceneName}");

                if (GameContext.IsInitialized)
                {
                    _eventSubscriber.EventBus.Publish(new PlayerLeftSceneEvent(playerInfo, scenelData));
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

