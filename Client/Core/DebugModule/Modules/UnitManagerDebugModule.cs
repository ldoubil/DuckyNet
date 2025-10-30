using System;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 单位管理调试模块 - 管理测试单位的创建和销毁
    /// </summary>
    public class UnitManagerDebugModule : IDebugModule
    {
        private Vector3 _spawnPosition = Vector3.zero;
        private string _unitName = "TestUnit";
        private int _unitTeam = 0;
        
        public string ModuleName => "单位管理";
        public string Category => "工具";
        public string Description => "创建和管理测试单位";
        public bool IsEnabled { get; set; } = true;

        public UnitManagerDebugModule()
        {
        }

        public void OnGUI()
        {
            if (!GameContext.IsInitialized)
            {
                GUILayout.Label("游戏上下文未初始化", GUI.skin.label);
                return;
            }

            var unitManager = GameContext.Instance.UnitManager;
            
            GUILayout.BeginVertical();

            // 单位统计
            GUILayout.Label("=== 单位统计 ===", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.Label($"当前单位数: {unitManager.UnitCount}");

            var units = unitManager.ManagedUnits;
            // 先复制列表，避免在遍历时集合被修改
            var unitsCopy = new System.Collections.Generic.List<GameObject>();
            foreach (var unit in units)
            {
                if (unit != null)
                {
                    unitsCopy.Add(unit);
                }
            }

            if (unitsCopy.Count > 0)
            {
                GUILayout.Label("单位列表:");
                for (int i = 0; i < Math.Min(unitsCopy.Count, 10); i++)
                {
                    if (unitsCopy[i] != null)
                    {
                        GUILayout.Label($"  [{i}] {unitsCopy[i].name}");
                    }
                }
                if (unitsCopy.Count > 10)
                {
                    GUILayout.Label($"  ... 还有 {unitsCopy.Count - 10} 个单位");
                }
            }

            GUILayout.Space(5);

            // 创建单位
            GUILayout.Label("=== 创建单位 ===", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("名称:", GUILayout.Width(50));
            _unitName = GUILayout.TextField(_unitName, GUILayout.Width(150));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("队伍:", GUILayout.Width(50));
            _unitTeam = (int)GUILayout.HorizontalSlider(_unitTeam, 0, 2);
            GUILayout.Label($"{_unitTeam}", GUILayout.Width(30));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("位置:", GUILayout.Width(50));
            var xStr = GUILayout.TextField(_spawnPosition.x.ToString("F1"), GUILayout.Width(60));
            var yStr = GUILayout.TextField(_spawnPosition.y.ToString("F1"), GUILayout.Width(60));
            var zStr = GUILayout.TextField(_spawnPosition.z.ToString("F1"), GUILayout.Width(60));
            
            // 安全解析浮点数
            if (float.TryParse(xStr, out float x)) _spawnPosition.x = x;
            if (float.TryParse(yStr, out float y)) _spawnPosition.y = y;
            if (float.TryParse(zStr, out float z)) _spawnPosition.z = z;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("创建单位"))
            {
                var stats = new Core.UnitStats();
                var unit = unitManager.CreateUnit(_unitName, _spawnPosition, _unitTeam, stats);
                if (unit != null)
                {
                    UnityEngine.Debug.Log($"[UnitManagerDebugModule] 创建单位成功: {_unitName}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[UnitManagerDebugModule] 创建单位失败: {_unitName}");
                }
            }
            
            if (GUILayout.Button("清理所有"))
            {
                // 使用复制的列表，避免遍历时集合被修改
                var unitsToDestroy = new System.Collections.Generic.List<GameObject>(unitManager.ManagedUnits);
                foreach (var unit in unitsToDestroy)
                {
                    if (unit != null)
                    {
                        unitManager.DestroyUnit(unit);
                    }
                }
                UnityEngine.Debug.Log("[UnitManagerDebugModule] 已清理所有单位");
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        public void Update()
        {
            // 可以在这里更新单位信息
        }
    }
}
