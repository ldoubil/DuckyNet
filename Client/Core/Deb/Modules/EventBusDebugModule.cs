using System;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Core.Deb
{
    /// <summary>
    /// 事件总线调试模块 - 显示 EventBus 状态和事件统计
    /// </summary>
    public class EventBusDebugModule : IDebugModule
    {
        private int _networkConnectedEvents = 0;
        private int _networkDisconnectedEvents = 0;
        private int _sceneLoadedEvents = 0;

        public string ModuleName => "事件总线";
        public string Category => "系统";
        public string Description => "显示 EventBus 事件统计";
        public bool IsEnabled { get; set; } = true;

        public EventBusDebugModule()
        {
            // 订阅事件来统计
            if (GameContext.IsInitialized)
            {
                var eventBus = GameContext.Instance.EventBus;
                eventBus.Subscribe<NetworkConnectedEvent>(_ => _networkConnectedEvents++);
                eventBus.Subscribe<NetworkDisconnectedEvent>(_ => _networkDisconnectedEvents++);
                eventBus.Subscribe<SceneLoadedEvent>(_ => _sceneLoadedEvents++);
            }
        }

        public void OnGUI()
        {
            if (!GameContext.IsInitialized)
            {
                GUILayout.Label("游戏上下文未初始化", GUI.skin.label);
                return;
            }

            var eventBus = GameContext.Instance.EventBus;
            
            GUILayout.BeginVertical();

            GUILayout.Label("=== 事件统计 ===", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

            GUILayout.Space(5);
            GUILayout.Label($"连接事件: {_networkConnectedEvents}");
            GUILayout.Label($"断开事件: {_networkDisconnectedEvents}");
            GUILayout.Label($"场景加载: {_sceneLoadedEvents}");

            GUILayout.Space(5);
            GUILayout.Label("=== 订阅者统计 ===", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

            GUILayout.Label($"NetworkConnected: {eventBus.GetSubscriberCount<NetworkConnectedEvent>()}");
            GUILayout.Label($"SceneLoaded: {eventBus.GetSubscriberCount<SceneLoadedEvent>()}");

            GUILayout.Space(5);
            if (GUILayout.Button("重置统计"))
            {
                _networkConnectedEvents = 0;
                _networkDisconnectedEvents = 0;
                _sceneLoadedEvents = 0;
            }

            GUILayout.EndVertical();
        }

        public void Update()
        {
            // 可以在这里更新事件统计
        }
    }
}
