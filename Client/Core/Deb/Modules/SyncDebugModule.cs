using System;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Core.Deb
{
    /// <summary>
    /// 同步调试模块 - 显示角色同步状态
    /// </summary>
    public class SyncDebugModule : IDebugModule
    {
        private float _lastSyncTime = 0f;
        private int _syncCount = 0;

        public string ModuleName => "同步状态";
        public string Category => "同步";
        public string Description => "显示角色同步的状态和统计";
        public bool IsEnabled { get; set; } = true;

        public SyncDebugModule()
        {
        }

        public void OnGUI()
        {
            if (!GameContext.IsInitialized)
            {
                GUILayout.Label("游戏上下文未初始化", GUI.skin.label);
                return;
            }

            var syncManager = GameContext.Instance.SyncManager;
            
            GUILayout.BeginVertical();

            if (syncManager == null)
            {
                GUILayout.Label("同步管理器未初始化", GUI.skin.label);
                GUILayout.EndVertical();
                return;
            }

            // 同步状态
            var statusStyle = new GUIStyle(GUI.skin.label);
            if (syncManager.IsEnabled)
            {
                statusStyle.normal.textColor = Color.green;
                GUILayout.Label($"● 同步已启用", statusStyle);
            }
            else
            {
                statusStyle.normal.textColor = Color.yellow;
                GUILayout.Label($"● 同步已停止", statusStyle);
            }

            // 同步统计
            GUILayout.Space(5);
            GUILayout.Label($"同步次数: {_syncCount}");
            
            if (_lastSyncTime > 0)
            {
                var timeSinceLastSync = Time.time - _lastSyncTime;
                GUILayout.Label($"上次同步: {timeSinceLastSync:F2} 秒前");
            }

            // 操作按钮
            GUILayout.Space(5);
            if (GUILayout.Button("立即同步"))
            {
                if (syncManager.IsEnabled)
                {
                    _ = syncManager.SyncNow();
                    _lastSyncTime = Time.time;
                    _syncCount++;
                }
            }

            if (GUILayout.Button("重置统计"))
            {
                _syncCount = 0;
                _lastSyncTime = 0f;
            }

            GUILayout.EndVertical();
        }

        public void Update()
        {
            // 可以在这里更新同步统计
            // 通过事件系统监听同步完成事件
        }
    }
}
