using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.UI;
using DuckyNet.Client.RPC;
using DuckyNet.Client.Services;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services.Generated;

namespace DuckyNet.Client.Core
{

    public class SceneClientManager : IDisposable
    {
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
        private SceneServiceClientProxy _sceneServiceClient;
        public ScenelData _scenelDataList = new ScenelData("", "");
        public SceneClientManager()
        {
            Debug.Log("[SceneClientManager] 构造函数开始");
            _eventSubscriber.EnsureInitializedAndSubscribe();
            _eventSubscriber.Subscribe<SceneLoadedDetailEvent>(OnSceneLoaded);
            _eventSubscriber.Subscribe<SceneUnloadingDetailEvent>(OnSceneUnloading);

            _eventSubscriber.Subscribe<PlayerEnteredSceneEvent>(OnPlayerEnteredScene);
            _eventSubscriber.Subscribe<PlayerLeftSceneEvent>(OnPlayerLeftScene);


            Debug.Log("[SceneClientManager] 构造函数完成 (事件已订阅)");
            var serverContext = new ClientServerContext(GameContext.Instance.RpcClient);
            _sceneServiceClient = new SceneServiceClientProxy(serverContext);
        }

        // 网络通知玩家离开场景事件
        private void OnPlayerLeftScene(PlayerLeftSceneEvent @event)
        {
            Debug.Log($"[SceneClientManager] 玩家离开场景: {@event.PlayerInfo.SteamName} {@event.ScenelData.SceneName} {@event.ScenelData.SubSceneName}");
            GameContext.Instance.RoomManager.RoomPlayers.ForEach(player => {
                if (player.SteamId == @event.PlayerInfo.SteamId)
                {
                    player.CurrentScenelData = new ScenelData("", "");
                }
            });
        }
        // 网络通知玩家进入场景事件
        private void OnPlayerEnteredScene(PlayerEnteredSceneEvent @event)
        {
            Debug.Log($"[SceneClientManager] 玩家进入场景: {@event.PlayerInfo.SteamName} {@event.ScenelData.SceneName} {@event.ScenelData.SubSceneName}");
            _scenelDataList = @event.ScenelData;
            GameContext.Instance.RoomManager.RoomPlayers.ForEach(player => {
                if (player.SteamId == @event.PlayerInfo.SteamId)
                {
                    player.CurrentScenelData = @event.ScenelData;
                }
            });
        }

        private void OnSceneLoaded(SceneLoadedDetailEvent evt)
        {
            Debug.Log($"[SceneClientManager] 场景加载: {evt.ScenelData.SceneName} {evt.ScenelData.SubSceneName}");
            _scenelDataList = evt.ScenelData;
            Debug.Log($"[SceneClientManager] 🔥 发送场景进入请求: {_scenelDataList.SceneName}");
            _sceneServiceClient.EnterSceneAsync(_scenelDataList);
        }

        private void OnSceneUnloading(SceneUnloadingDetailEvent evt)
        {
            Debug.Log($"[SceneClientManager] 场景卸载: {evt.ScenelData.SceneName} {evt.ScenelData.SubSceneName}");
            _sceneServiceClient.LeaveSceneAsync(_scenelDataList);
            _scenelDataList = new ScenelData("", "");
        }

        public void Dispose()
        {
            _scenelDataList = new ScenelData("", "");
            _eventSubscriber.Dispose();
        }
    }
}