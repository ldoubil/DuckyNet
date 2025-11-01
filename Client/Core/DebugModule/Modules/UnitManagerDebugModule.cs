using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 远程玩家调试模块 - 在玩家附近创建测试远程玩家
    /// </summary>
    public class UnitManagerDebugModule : IDebugModule
    {
        private int _testPlayerIndex = 0;
        private int _createCount = 1; // 指定创建数量
        
        public string ModuleName => "远程玩家测试";
        public string Category => "测试";
        public string Description => "在玩家附近创建测试远程玩家";
        public bool IsEnabled { get; set; } = true;

        public UnitManagerDebugModule()
        {
        }

        public void OnGUI()
        {
            if (!GameContext.IsInitialized)
            {
                GUILayout.Label("❌ GameContext 未初始化", GUI.skin.label);
                return;
            }

            var unitManager = GameContext.Instance.UnitManager;
            
            GUILayout.BeginVertical("box");
            GUILayout.Label("👥 远程玩家测试", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            
            GUILayout.Label($"当前远程玩家数: {unitManager.RemotePlayerCount}");

            // 创建数量输入
            GUILayout.BeginHorizontal();
            GUILayout.Label("创建数量:", GUILayout.Width(60));
            var countStr = GUILayout.TextField(_createCount.ToString(), GUILayout.Width(50));
            if (int.TryParse(countStr, out int count))
            {
                _createCount = Mathf.Clamp(count, 1, 50); // 限制 1-50 个
            }
            GUILayout.Label($"(1-50)", GUILayout.Width(50));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"创建 {_createCount} 个玩家", GUILayout.Height(40)))
            {
                for (int i = 0; i < _createCount; i++)
                {
                    CreateTestRemotePlayer(unitManager);
                    UnityEngine.Time.timeScale += 0; // 保证每帧只创建一个
                }
            }
            
            if (GUILayout.Button("清空所有", GUILayout.Height(40)))
            {
                unitManager.DestroyAllRemotePlayers();
                UnityEngine.Debug.Log("[UnitManagerDebugModule] 已清空所有远程玩家");
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void CreateTestRemotePlayer(UnitManager unitManager)
        {
            // 在玩家附近随机位置创建
            Vector3 spawnPos = Vector3.zero;
            
            try
            {
                // 尝试获取玩家位置
                var levelManagerType = HarmonyLib.AccessTools.TypeByName("LevelManager");
                if (levelManagerType != null)
                {
                    var instanceProp = HarmonyLib.AccessTools.Property(levelManagerType, "Instance");
                    object? levelManager = instanceProp?.GetValue(null);
                    
                    if (levelManager != null)
                    {
                        var mainCharProp = HarmonyLib.AccessTools.Property(levelManagerType, "MainCharacter");
                        object? mainChar = mainCharProp?.GetValue(levelManager);
                        
                        if (mainChar is Component component)
                        {
                            spawnPos = component.transform.position + UnityEngine.Random.insideUnitSphere * 5f;
                            spawnPos.y = 1f;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[UnitManagerDebugModule] 获取玩家位置失败: {ex.Message}");
                spawnPos = UnityEngine.Random.insideUnitSphere * 5f;
                spawnPos.y = 1f;
            }

            var playerId = $"TestPlayer_{_testPlayerIndex++}";
            var player = unitManager.CreateRemotePlayer(playerId, spawnPos);
            
            if (player != null)
            {
                UnityEngine.Debug.Log($"[UnitManagerDebugModule] ✅ 创建测试玩家: {playerId} 在 {spawnPos}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[UnitManagerDebugModule] ❌ 创建失败: {playerId}");
            }
        }

        public void Update()
        {
        }
    }
}
