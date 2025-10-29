using System;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Core.Deb
{
    /// <summary>
    /// 场景调试模块 - 显示当前场景信息
    /// </summary>
    public class SceneDebugModule : IDebugModule
    {
        public string ModuleName => "场景信息";
        public string Category => "场景";
        public string Description => "显示当前场景名称和玩家信息";
        public bool IsEnabled { get; set; } = true;

        public SceneDebugModule()
        {
        }

        public void OnGUI()
        {
            if (!GameContext.IsInitialized)
            {
                GUILayout.Label("游戏上下文未初始化", GUI.skin.label);
                return;
            }

            var sceneManager = GameContext.Instance.SceneManager;
            
            GUILayout.BeginVertical();

            // 当前场景
            var currentScene = sceneManager.CurrentScene ?? "无";
            GUILayout.Label($"当前场景: {currentScene}");

            // 场景状态
            GUILayout.Label($"切换中: {(sceneManager.IsChangingScene ? "是" : "否")}");

            // 玩家场景信息
            GUILayout.Space(5);
            var allPlayers = sceneManager.GetAllPlayerScenes();
            GUILayout.Label($"场景内玩家数: {allPlayers.Count}");

            if (GUILayout.Button("刷新"))
            {
                // 可以触发刷新操作
            }

            GUILayout.EndVertical();
        }

        public void Update()
        {
            // 可以在这里更新场景信息
        }
    }
}
