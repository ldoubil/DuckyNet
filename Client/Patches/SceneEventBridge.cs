using System;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;
using UnityEngine;
using Duckov.Scenes;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 场景事件桥接器：仅负责转发进入/离开地图事件到全局 EventBus。
    /// </summary>
    public class SceneEventBridge : IDisposable
    {
        public void Initialize()
        {
            try
            {
                MultiSceneCore.OnSubSceneWillBeUnloaded += OnSubSceneWillBeUnloaded;
                MultiSceneCore.OnSubSceneLoaded += OnSubSceneLoaded;
                Debug.Log("[SceneEventBridge] 已订阅场景事件");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneEventBridge] 订阅场景事件失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                MultiSceneCore.OnSubSceneWillBeUnloaded -= OnSubSceneWillBeUnloaded;
                MultiSceneCore.OnSubSceneLoaded -= OnSubSceneLoaded;
            }
            catch { }
        }

        private void OnSubSceneWillBeUnloaded(MultiSceneCore core, UnityEngine.SceneManagement.Scene scene)
        {
            try
            {
                if (!GameContext.IsInitialized) return;

                string mainSceneName = core.gameObject.scene.name;
                string subSceneName = scene.name;

                Debug.Log($"[SceneEventBridge] 子场景即将卸载: {subSceneName}");
                GameContext.Instance.EventBus.Publish(new SceneUnloadingDetailEvent(new ScenelData(mainSceneName, subSceneName)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneEventBridge] OnSubSceneWillBeUnloaded 失败: {ex.Message}");
            }
        }

        private void OnSubSceneLoaded(MultiSceneCore core, UnityEngine.SceneManagement.Scene scene)
        {
            try
            {
                if (!GameContext.IsInitialized) return;

                string mainSceneName = core.gameObject.scene.name;
                string subSceneName = scene.name;

                Debug.Log($"[SceneEventBridge] 子场景加载完成: {subSceneName}");
                GameContext.Instance.EventBus.Publish(new SceneLoadedDetailEvent(new ScenelData(mainSceneName, subSceneName)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneEventBridge] OnSubSceneLoaded 失败: {ex.Message}");
            }
        }
    }
}




