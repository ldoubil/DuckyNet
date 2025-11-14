using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.UI;
using DuckyNet.RPC;
using DuckyNet.Client.RPC;
using DuckyNet.Client.Services;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services.Generated;
using DuckyNet.Client.Core.EventBus;

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
            
            // 🔥 订阅本地场景事件（Unity场景系统触发）
            _eventSubscriber.Subscribe<SceneLoadedDetailEvent>(OnSceneLoaded);
            _eventSubscriber.Subscribe<SceneUnloadingDetailEvent>(OnSceneUnloading);

            // ❌ 移除：服务器不再发送这些事件，改用位置同步触发角色创建
            // _eventSubscriber.Subscribe<PlayerEnteredSceneEvent>(OnPlayerEnteredScene);
            // _eventSubscriber.Subscribe<PlayerLeftSceneEvent>(OnPlayerLeftScene);

            Debug.Log("[SceneClientManager] 构造函数完成 (事件已订阅)");
            var serverContext = new ClientServerContext(GameContext.Instance.RpcClient);
            _sceneServiceClient = new SceneServiceClientProxy(serverContext);
        }

        // ❌ 已移除：服务器不再发送这些事件
        // 角色创建/销毁改由 RemotePlayer 监听位置同步事件自动处理

        private void OnSceneLoaded(SceneLoadedDetailEvent evt)
        {
            Debug.Log($"[SceneClientManager] 场景加载: {evt.ScenelData.SceneName} {evt.ScenelData.SubSceneName}");
            _scenelDataList = evt.ScenelData;
            Debug.Log($"[SceneClientManager] 🔥 发送场景进入请求: {_scenelDataList.SceneName}");
            _sceneServiceClient.EnterSceneAsync(_scenelDataList);
            
            // 🔥 场景加载完成后,刷新房间玩家列表,获取其他玩家位置
            // 这样可以在新场景中重新创建其他玩家的角色
            if (GameContext.IsInitialized && GameContext.Instance.RoomManager?.CurrentRoom != null)
            {
                Debug.Log($"[SceneClientManager] 场景加载完成,刷新房间玩家列表");
                GameContext.Instance.RoomManager.RefreshPlayerListAsync();
            }
        }

        private void OnSceneUnloading(SceneUnloadingDetailEvent evt)
        {
            Debug.Log($"[SceneClientManager] 场景卸载: {evt.ScenelData.SceneName} {evt.ScenelData.SubSceneName}");
            
            // 🔥 修复：使用事件中的场景数据（即将卸载的场景），而不是 _scenelDataList
            // 因为 _scenelDataList 可能已经被新场景更新了（OnSceneLoaded先执行）
            _sceneServiceClient.LeaveSceneAsync(evt.ScenelData);
            
            Debug.Log($"[SceneClientManager] ✅ 已发送离开场景请求: {evt.ScenelData.SceneName}");
            
            // 只有在离开主场景时才清空（子场景切换不清空）
            // 🔥 修复：检查是否是主场景卸载
            if (evt.ScenelData.SceneName == _scenelDataList.SceneName)
            {
                Debug.Log($"[SceneClientManager] 主场景卸载，清空场景数据");
                _scenelDataList = new ScenelData("", "");
            }
        }

        public void Dispose()
        {
            _scenelDataList = new ScenelData("", "");
            _eventSubscriber.Dispose();
        }
    }
}