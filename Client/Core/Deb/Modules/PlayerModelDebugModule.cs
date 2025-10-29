using System;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Core.Deb
{
    /// <summary>
    /// 玩家模型调试模块 - 显示和管理玩家模型状态
    /// </summary>
    public class PlayerModelDebugModule : IDebugModule
    {
        public string ModuleName => "玩家模型";
        public string Category => "场景";
        public string Description => "显示玩家模型管理状态";
        public bool IsEnabled { get; set; } = true;

        public PlayerModelDebugModule()
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

            GUILayout.Label("=== 玩家模型信息 ===", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

            // 获取所有玩家场景信息
            var allPlayers = sceneManager.GetAllPlayerScenes();
            GUILayout.Label($"总玩家数: {allPlayers.Count}");

            var currentScene = sceneManager.CurrentScene;
            if (!string.IsNullOrEmpty(currentScene))
            {
                var playersInScene = sceneManager.GetPlayersInScene(currentScene);
                GUILayout.Label($"当前场景玩家: {playersInScene.Count}");

                if (playersInScene.Count > 0)
                {
                    GUILayout.Space(3);
                    GUILayout.Label("玩家列表:");
                    foreach (var player in playersInScene)
                    {
                        var playerName = player.PlayerInfo?.SteamName ?? "Unknown";
                        var hasModel = player.HasCharacter ? "有模型" : "无模型";
                        GUILayout.Label($"  • {playerName} ({hasModel})");
                    }
                }
            }
            else
            {
                GUILayout.Label("当前不在场景中", GUI.skin.label);
            }

            GUILayout.Space(5);

            if (GUILayout.Button("刷新"))
            {
                // 可以触发刷新操作
                UnityEngine.Debug.Log("[PlayerModelDebugModule] 已刷新玩家模型信息");
            }

            GUILayout.EndVertical();
        }

        public void Update()
        {
            // 可以在这里更新玩家模型信息
        }
    }
}
