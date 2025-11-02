using System;
using UnityEngine;
using DuckyNet.Client.Core.Utils;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 外观测试调试模块 - 在本地玩家旁边创建测试单位并复制外观
    /// </summary>
    public class AppearanceTestModule : IDebugModule
    {
        public string ModuleName => "外观测试";
        public string Category => "测试";
        public string Description => "在本地玩家旁边创建测试单位并复制外观数据";
        public bool IsEnabled { get; set; } = false;

        private object? _testCharacter;
        private string _statusInfo = "";
        private Vector3 _spawnOffset = new Vector3(2f, 0f, 0f); // 默认在右侧2米
        private CharacterAppearanceData? _cachedAppearance;

        public void OnGUI()
        {
            if (!IsEnabled) return;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("=== 外观测试工具 ===", GUI.skin.box);
            
            // 偏移量设置
            GUILayout.BeginHorizontal();
            GUILayout.Label("生成偏移 X:", GUILayout.Width(80));
            if (float.TryParse(GUILayout.TextField(_spawnOffset.x.ToString("F1"), GUILayout.Width(60)), out float x))
                _spawnOffset.x = x;
            GUILayout.Label("Y:", GUILayout.Width(20));
            if (float.TryParse(GUILayout.TextField(_spawnOffset.y.ToString("F1"), GUILayout.Width(60)), out float y))
                _spawnOffset.y = y;
            GUILayout.Label("Z:", GUILayout.Width(20));
            if (float.TryParse(GUILayout.TextField(_spawnOffset.z.ToString("F1"), GUILayout.Width(60)), out float z))
                _spawnOffset.z = z;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 按钮区域
            if (GUILayout.Button("1️⃣ 获取本地玩家外观", GUILayout.Height(40)))
            {
                CaptureLocalPlayerAppearance();
            }

            if (GUILayout.Button("2️⃣ 创建测试单位", GUILayout.Height(40)))
            {
                CreateTestCharacter();
            }

            if (GUILayout.Button("3️⃣ 应用外观到测试单位", GUILayout.Height(40)))
            {
                ApplyAppearanceToTestCharacter();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("🔄 一键创建并复制外观", GUILayout.Height(50)))
            {
                QuickCreateAndCopy();
            }

            GUILayout.Space(10);

            if (_testCharacter != null && GUILayout.Button("🗑️ 删除测试单位", GUILayout.Height(40)))
            {
                DestroyTestCharacter();
            }

            // 状态信息
            GUILayout.Space(10);
            GUILayout.Box(_statusInfo, GUILayout.ExpandHeight(true));
            
            GUILayout.EndVertical();
        }

        /// <summary>
        /// 获取本地玩家外观数据
        /// </summary>
        private void CaptureLocalPlayerAppearance()
        {
            try
            {
                var mainChar = CharacterMainControl.Main;
                if (mainChar == null)
                {
                    _statusInfo = "❌ 无法获取本地玩家";
                    return;
                }

                _cachedAppearance = AppearanceConverter.GetCharacterAppearance(mainChar);
                if (_cachedAppearance != null)
                {
                    _statusInfo = $"✅ 成功获取本地玩家外观\n部件数量: {_cachedAppearance.Parts.Length}";
                }
                else
                {
                    _statusInfo = "❌ 获取外观数据失败";
                }
            }
            catch (Exception ex)
            {
                _statusInfo = $"❌ 获取外观异常:\n{ex.Message}";
                Debug.LogError($"[AppearanceTestModule] {ex}");
            }
        }

        /// <summary>
        /// 创建测试单位
        /// </summary>
        private void CreateTestCharacter()
        {
            try
            {
                var mainChar = CharacterMainControl.Main;
                if (mainChar == null)
                {
                    _statusInfo = "❌ 无法获取本地玩家位置";
                    return;
                }

                // 计算生成位置（在本地玩家旁边）
                var playerPos = mainChar.transform.position;
                var spawnPos = playerPos + _spawnOffset;

                _statusInfo = "⏳ 正在创建测试单位...";

                // 1. 创建角色数据项
                var characterItem = CharacterCreationUtils.CreateCharacterItem();
                if (characterItem == null)
                {
                    _statusInfo = "❌ 创建角色数据项失败";
                    return;
                }

                // 2. 获取角色模型预制体
                var modelPrefab = CharacterCreationUtils.GetCharacterModelPrefab();
                if (modelPrefab == null)
                {
                    _statusInfo = "❌ 获取角色模型预制体失败";
                    return;
                }

                // 3. 实例化角色
                var newCharacter = CharacterCreationUtils.CreateCharacterInstance(
                    characterItem, modelPrefab, spawnPos, Quaternion.identity
                );
                if (newCharacter == null)
                {
                    _statusInfo = "❌ 实例化角色失败";
                    return;
                }

                // 4. 配置角色
                CharacterCreationUtils.ConfigureCharacter(newCharacter, "TestCharacter_Appearance", spawnPos, team: 0);
                CharacterCreationUtils.ConfigureCharacterPreset(newCharacter, "测试单位", showName: true);

                // 5. 标记为远程玩家（禁用移动）
                CharacterCreationUtils.MarkAsRemotePlayer(newCharacter);

                // 6. 从距离系统移除
                CharacterCreationUtils.UnregisterFromDistanceSystem(newCharacter);

                // 7. 请求血条
                CharacterCreationUtils.RequestHealthBar(newCharacter, "测试单位", null);

                _testCharacter = newCharacter;
                _statusInfo = $"✅ 测试单位创建成功\n位置: {spawnPos}";
            }
            catch (Exception ex)
            {
                _statusInfo = $"❌ 创建测试单位异常:\n{ex.Message}";
                Debug.LogError($"[AppearanceTestModule] {ex}");
            }
        }

        /// <summary>
        /// 应用外观到测试单位
        /// </summary>
        private void ApplyAppearanceToTestCharacter()
        {
            try
            {
                if (_testCharacter == null)
                {
                    _statusInfo = "❌ 测试单位不存在\n请先创建测试单位";
                    return;
                }

                if (_cachedAppearance == null)
                {
                    _statusInfo = "❌ 没有缓存的外观数据\n请先获取本地玩家外观";
                    return;
                }

                _statusInfo = "⏳ 正在应用外观...";

                // 延迟应用外观（等待角色初始化）
                if (ModBehaviour.Instance != null)
                {
                    ModBehaviour.Instance.StartCoroutine(ApplyAppearanceDelayed());
                }
                else
                {
                    // 直接应用
                    ApplyAppearanceNow();
                }
            }
            catch (Exception ex)
            {
                _statusInfo = $"❌ 应用外观异常:\n{ex.Message}";
                Debug.LogError($"[AppearanceTestModule] {ex}");
            }
        }

        /// <summary>
        /// 延迟应用外观
        /// </summary>
        private System.Collections.IEnumerator ApplyAppearanceDelayed()
        {
            // 等待 2 帧
            yield return null;
            yield return null;

            ApplyAppearanceNow();
        }

        /// <summary>
        /// 立即应用外观
        /// </summary>
        private void ApplyAppearanceNow()
        {
            if (_testCharacter == null || _cachedAppearance == null)
            {
                _statusInfo = "❌ 测试单位或外观数据为空";
                return;
            }

            bool success = AppearanceConverter.ApplyAppearanceToCharacter(_testCharacter!, _cachedAppearance);
            if (success)
            {
                _statusInfo = "✅ 外观应用成功！";
            }
            else
            {
                _statusInfo = "❌ 外观应用失败\n查看日志了解详情";
            }
        }

        /// <summary>
        /// 一键创建并复制外观
        /// </summary>
        private void QuickCreateAndCopy()
        {
            try
            {
                // 1. 获取外观
                CaptureLocalPlayerAppearance();
                if (_cachedAppearance == null)
                {
                    return;
                }

                // 2. 创建角色
                CreateTestCharacter();
                if (_testCharacter == null)
                {
                    return;
                }

                // 3. 应用外观
                ApplyAppearanceToTestCharacter();
            }
            catch (Exception ex)
            {
                _statusInfo = $"❌ 一键创建异常:\n{ex.Message}";
                Debug.LogError($"[AppearanceTestModule] {ex}");
            }
        }

        /// <summary>
        /// 删除测试单位
        /// </summary>
        private void DestroyTestCharacter()
        {
            try
            {
                if (_testCharacter != null && _testCharacter is Component component)
                {
                    UnityEngine.Object.Destroy(component.gameObject);
                    _testCharacter = null;
                    _statusInfo = "✅ 测试单位已删除";
                }
            }
            catch (Exception ex)
            {
                _statusInfo = $"❌ 删除失败:\n{ex.Message}";
                Debug.LogError($"[AppearanceTestModule] {ex}");
            }
        }

        public void OnDisable()
        {
            // 模块禁用时可选择清理
        }

        public void OnEnable()
        {
            _statusInfo = "使用步骤:\n1. 获取本地玩家外观\n2. 创建测试单位\n3. 应用外观\n\n或直接点击一键按钮";
        }

        public void Update()
        {
            // 不需要每帧更新
        }
    }
}
