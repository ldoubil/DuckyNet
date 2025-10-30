using System;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Core.DebugModule
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

            
            GUILayout.BeginVertical();


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
