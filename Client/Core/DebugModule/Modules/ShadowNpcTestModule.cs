using System;
using UnityEngine;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core.DebugModule.Modules
{
    /// <summary>
    /// 影子 NPC 测试模块 - 用于测试影子 NPC 的创建和管理
    /// </summary>
    public class ShadowNpcTestModule : IDebugModule
    {
        public string ModuleName => "影子 NPC 测试";
        public string Category => "测试工具";
        public string Description => "在玩家附近创建测试用的影子 NPC";
        public bool IsEnabled { get; set; } = true;

        private string _npcType = "Character(Clone)";
        private float _distance = 5f;
        private float _angle = 0f;
        private string _lastCreatedId = "";
        private GameObject? _lastCreatedNpc = null;

        public void Update()
        {
            // 模块不需要每帧更新
        }

        public void OnGUI()
        {
            GUILayout.Label("=== 影子 NPC 创建测试 ===");

            // NPC 类型
            GUILayout.BeginHorizontal();
            GUILayout.Label("NPC 类型:", GUILayout.Width(100));
            _npcType = GUILayout.TextField(_npcType, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            // 距离
            GUILayout.BeginHorizontal();
            GUILayout.Label($"距离: {_distance:F1}m", GUILayout.Width(100));
            _distance = GUILayout.HorizontalSlider(_distance, 1f, 20f, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            // 角度
            GUILayout.BeginHorizontal();
            GUILayout.Label($"角度: {_angle:F0}°", GUILayout.Width(100));
            _angle = GUILayout.HorizontalSlider(_angle, 0f, 360f, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 创建按钮
            if (GUILayout.Button("🎭 在玩家附近创建影子 NPC", GUILayout.Height(30)))
            {
                CreateShadowNpcNearPlayer();
            }

            GUILayout.Space(5);

            // 创建多个测试按钮
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("创建 3 个 NPC"))
            {
                CreateMultipleShadowNpcs(3);
            }
            if (GUILayout.Button("创建 5 个 NPC"))
            {
                CreateMultipleShadowNpcs(5);
            }
            if (GUILayout.Button("创建环形 (8个)"))
            {
                CreateCircleOfNpcs(8, 5f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 最后创建的 NPC 信息
            if (!string.IsNullOrEmpty(_lastCreatedId))
            {
                GUILayout.Label("=== 最后创建的 NPC ===");
                GUILayout.Label($"ID: {_lastCreatedId}");
                GUILayout.Label($"状态: {(_lastCreatedNpc != null ? "存在" : "已销毁")}");

                if (_lastCreatedNpc != null)
                {
                    var pos = _lastCreatedNpc.transform.position;
                    GUILayout.Label($"位置: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");

                    if (GUILayout.Button("🗑️ 销毁此 NPC", GUILayout.Height(25)))
                    {
                        DestroyShadowNpc();
                    }
                }
            }

            GUILayout.Space(10);

            // 清理所有按钮
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("🧹 清理所有影子 NPC", GUILayout.Height(30)))
            {
                ClearAllShadowNpcs();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(5);
            GUILayout.Label($"💡 提示: 影子 NPC 没有 AI，仅用于测试网络同步");
        }

        /// <summary>
        /// 在玩家附近创建影子 NPC
        /// </summary>
        private void CreateShadowNpcNearPlayer()
        {
            try
            {
                var player = GameContext.Instance.PlayerManager?.LocalPlayer;
                if (player?.CharacterObject == null)
                {
                    Debug.LogWarning("[ShadowNpcTest] 本地玩家不存在");
                    return;
                }

                // 计算创建位置（基于角度和距离）
                var playerPos = player.CharacterObject.transform.position;
                var playerRot = player.CharacterObject.transform.rotation.eulerAngles.y;
                
                float radians = (_angle + playerRot) * Mathf.Deg2Rad;
                Vector3 spawnPos = new Vector3(
                    playerPos.x + _distance * Mathf.Sin(radians),
                    playerPos.y,
                    playerPos.z + _distance * Mathf.Cos(radians)
                );

                // 创建 NPC 数据
                var npcData = new NpcSpawnData
                {
                    NpcId = Guid.NewGuid().ToString(),
                    NpcType = _npcType,
                    SceneName = player.Info?.CurrentScenelData?.SceneName ?? "",
                    SubSceneName = player.Info?.CurrentScenelData?.SubSceneName ?? "",
                    PositionX = spawnPos.x,
                    PositionY = spawnPos.y,
                    PositionZ = spawnPos.z,
                    RotationY = UnityEngine.Random.Range(0f, 360f),
                    MaxHealth = 100f
                };

                // 创建影子 NPC
                var shadowNpc = ShadowNpcFactory.CreateShadowNpc(npcData);
                if (shadowNpc != null && shadowNpc is Component comp)
                {
                    _lastCreatedId = npcData.NpcId;
                    _lastCreatedNpc = comp.gameObject;

                    // 也添加到 NpcManager（作为远程 NPC）
                    GameContext.Instance.NpcManager?.AddRemoteNpc(
                        npcData.NpcId,
                        shadowNpc,
                        comp.gameObject,
                        npcData.NpcType,
                        npcData.SceneName,
                        npcData.SubSceneName
                    );

                    Debug.Log($"[ShadowNpcTest] ✅ 创建成功: {_npcType} at ({spawnPos.x:F2}, {spawnPos.y:F2}, {spawnPos.z:F2})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShadowNpcTest] 创建失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建多个影子 NPC
        /// </summary>
        private void CreateMultipleShadowNpcs(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _angle = UnityEngine.Random.Range(0f, 360f);
                _distance = UnityEngine.Random.Range(3f, 10f);
                CreateShadowNpcNearPlayer();
            }
        }

        /// <summary>
        /// 创建环形分布的 NPC
        /// </summary>
        private void CreateCircleOfNpcs(int count, float radius)
        {
            float angleStep = 360f / count;
            for (int i = 0; i < count; i++)
            {
                _angle = i * angleStep;
                _distance = radius;
                CreateShadowNpcNearPlayer();
            }
        }

        /// <summary>
        /// 销毁最后创建的 NPC
        /// </summary>
        private void DestroyShadowNpc()
        {
            if (_lastCreatedNpc != null)
            {
                // 从 NpcManager 移除
                if (!string.IsNullOrEmpty(_lastCreatedId))
                {
                    GameContext.Instance.NpcManager?.RemoveRemoteNpc(_lastCreatedId);
                }

                UnityEngine.Object.Destroy(_lastCreatedNpc);
                _lastCreatedNpc = null;
                Debug.Log("[ShadowNpcTest] 已销毁 NPC");
            }
        }

        /// <summary>
        /// 清理所有影子 NPC
        /// </summary>
        private void ClearAllShadowNpcs()
        {
            try
            {
                var markers = UnityEngine.Object.FindObjectsOfType<ShadowNpcMarker>();
                int count = 0;

                foreach (var marker in markers)
                {
                    if (marker != null && marker.gameObject != null)
                    {
                        // 从 NpcManager 移除
                        GameContext.Instance.NpcManager?.RemoveRemoteNpc(marker.NpcId);
                        
                        UnityEngine.Object.Destroy(marker.gameObject);
                        count++;
                    }
                }

                _lastCreatedNpc = null;
                _lastCreatedId = "";

                Debug.Log($"[ShadowNpcTest] 已清理 {count} 个影子 NPC");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShadowNpcTest] 清理失败: {ex.Message}");
            }
        }
    }
}

