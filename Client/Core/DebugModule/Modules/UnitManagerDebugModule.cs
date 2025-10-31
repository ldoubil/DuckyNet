using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 远程玩家调试模块 - 在玩家附近创建测试远程玩家
    /// 创建后自动平滑移动，高度保持不变
    /// </summary>
    public class UnitManagerDebugModule : IDebugModule
    {
        private int _testPlayerIndex = 0;
        
        /// <summary>
        /// 追踪远程玩家的移动状态
        /// </summary>
        private class RemotePlayerMovement
        {
            public GameObject? GameObject { get; set; }
            public Vector3 StartPosition { get; set; }
            public Vector3 TargetPosition { get; set; }
            public float MoveTimer { get; set; }
            public float MoveDuration { get; set; }
            public float FixedHeight { get; set; } // 记录原始高度
            public bool IsDancing { get; set; } = false; // 蹦迪标志
        }

        private List<RemotePlayerMovement> _movingPlayers = new List<RemotePlayerMovement>();
        private int _createCount = 1; // 指定创建数量
        private bool _danceModeEnabled = false; // 蹦迪模式开关
        private float _danceFrequency = 3f; // 蹦迪频率（Hz）
        private float _danceAmplitude = 3f; // 蹦迪幅度（米）
        
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
            GUILayout.Label($"移动中的玩家: {_movingPlayers.Count}");

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
                _movingPlayers.Clear();
                UnityEngine.Debug.Log("[UnitManagerDebugModule] 已清空所有远程玩家");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 蹦迪模式控制
            GUILayout.Label("🎵 蹦迪模式", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            
            _danceModeEnabled = GUILayout.Toggle(_danceModeEnabled, $"启用蹦迪: {(_danceModeEnabled ? "✓" : "✗")}");
            
            if (_danceModeEnabled)
            {
                // 频率控制
                GUILayout.BeginHorizontal();
                GUILayout.Label("频率 (Hz):", GUILayout.Width(80));
                var freqStr = GUILayout.TextField(_danceFrequency.ToString("F1"), GUILayout.Width(60));
                if (float.TryParse(freqStr, out float freq))
                {
                    _danceFrequency = Mathf.Clamp(freq, 0.5f, 10f);
                }
                GUILayout.Label($"(0.5-10)", GUILayout.Width(70));
                GUILayout.EndHorizontal();
                
                // 幅度控制
                GUILayout.BeginHorizontal();
                GUILayout.Label("幅度 (m):", GUILayout.Width(80));
                var ampStr = GUILayout.TextField(_danceAmplitude.ToString("F2"), GUILayout.Width(60));
                if (float.TryParse(ampStr, out float amp))
                {
                    _danceAmplitude = Mathf.Clamp(amp, 0.1f, 1f);
                }
                GUILayout.Label($"(0.1-1.0)", GUILayout.Width(70));
                GUILayout.EndHorizontal();
            }

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
                // 禁用物理组件防止下落
                var rigidbody = player.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    rigidbody.isKinematic = true;
                    rigidbody.useGravity = false;
                    UnityEngine.Debug.Log($"[UnitManagerDebugModule] 已禁用 {playerId} 的物理组件");
                }

                UnityEngine.Debug.Log($"[UnitManagerDebugModule] ✅ 创建测试玩家: {playerId} 在 {spawnPos}");
                
                // 配置角色名字显示
                ConfigureCharacterName(player, playerId);
                
                // 添加到移动列表
                var targetPos = spawnPos + UnityEngine.Random.insideUnitSphere * 8f;
                targetPos.y = spawnPos.y;
                
                _movingPlayers.Add(new RemotePlayerMovement
                {
                    GameObject = player,
                    StartPosition = spawnPos,
                    TargetPosition = targetPos,
                    MoveTimer = 0f,
                    MoveDuration = UnityEngine.Random.Range(2f, 5f), // 随机移动时间 2-5 秒
                    FixedHeight = spawnPos.y, // 记录原始高度
                    IsDancing = _danceModeEnabled // 继承当前蹦迪模式
                });
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[UnitManagerDebugModule] ❌ 创建失败: {playerId}");
            }
        }

        private void ConfigureCharacterName(GameObject player, string playerId)
        {
            var characterNameText = player.GetComponent<TextMesh>();
            if (characterNameText == null)
            {
                characterNameText = player.AddComponent<TextMesh>();
                characterNameText.characterSize = 0.5f;
                characterNameText.fontSize = 16;
                characterNameText.anchor = TextAnchor.MiddleCenter;
                characterNameText.alignment = TextAlignment.Center;
                characterNameText.color = Color.white;
                characterNameText.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f); // 调整大小
            }
            characterNameText.text = playerId;
            characterNameText.transform.position = player.transform.position + new Vector3(0, 1.5f, 0); // 调整位置
        }

        public void Update()
        {
            // 更新所有移动中的远程玩家
            for (int i = _movingPlayers.Count - 1; i >= 0; i--)
            {
                var movement = _movingPlayers[i];
                
                // 检查对象是否仍然存在
                if (movement.GameObject == null || !movement.GameObject.activeInHierarchy)
                {
                    _movingPlayers.RemoveAt(i);
                    continue;
                }

                // 更新计时器
                movement.MoveTimer += Time.deltaTime;
                
                if (movement.MoveTimer >= movement.MoveDuration)
                {
                    // 到达目标位置，生成新的目标
                    movement.StartPosition = movement.GameObject.transform.position;
                    var randomOffset = UnityEngine.Random.insideUnitSphere * 8f;
                    var newTarget = movement.StartPosition + randomOffset;
                    newTarget.y = movement.FixedHeight; // 使用记录的原始高度
                    movement.TargetPosition = newTarget;
                    movement.MoveTimer = 0f;
                    movement.MoveDuration = UnityEngine.Random.Range(2f, 5f);
                }
                
                // 计算插值进度 (0-1)
                float progress = movement.MoveTimer / movement.MoveDuration;
                
                // 使用平滑缓动曲线（三次平方缓动）
                float smoothProgress = progress < 0.5f
                    ? 2f * progress * progress  // 加速阶段
                    : 1f - (float)Math.Pow(-2f * progress + 2f, 2f) / 2f; // 减速阶段
                
                // 平滑插值位置
                Vector3 newPos = Vector3.Lerp(movement.StartPosition, movement.TargetPosition, smoothProgress);
                newPos.y = movement.FixedHeight; // 确保高度保持不变
                
                // 蹦迪效果：上下抽搐
                if (movement.IsDancing && _danceModeEnabled)
                {
                    float danceWave = (float)Math.Sin(Time.time * _danceFrequency * 2f * Mathf.PI);
                    newPos.y += danceWave * _danceAmplitude;
                }
                
                movement.GameObject.transform.position = newPos;
            }
        }
    }
}
