using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using DuckyNet.Shared.Data;
using DuckyNet.Client.Core.Utils;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 角色外观调试模块
    /// 用于查看和导出本地玩家及远程玩家的外观数据
    /// </summary>
    public class CharacterAppearanceDebugModule : IDebugModule
    {
        private Vector2 _scrollPosition;
        private string _appearanceInfo = "";
        private bool _autoRefresh = false;
        private float _refreshTimer = 0f;
        private const float REFRESH_INTERVAL = 1f;
        private CharacterAppearanceData? _cachedAppearanceData = null;

        public string ModuleName => "角色外观调试";
        public string Category => "角色";
        public string Description => "查看、导出和导入角色外观数据";
        public bool IsEnabled { get; set; } = false;

        public void OnGUI()
        {
            if (!IsEnabled) return;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("=== 角色外观调试 ===", GUI.skin.box);

            // 控制按钮
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("获取本地玩家外观", GUILayout.Width(150)))
            {
                GetLocalPlayerAppearance();
            }
            
            if (GUILayout.Button("应用到本地玩家", GUILayout.Width(130)))
            {
                ApplyToLocalPlayer();
            }
            
            if (GUILayout.Button("清空", GUILayout.Width(80)))
            {
                _appearanceInfo = "";
                _cachedAppearanceData = null;
            }
            
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "自动刷新", GUILayout.Width(100));
            GUILayout.EndHorizontal();
            
            // 数据信息
            if (_cachedAppearanceData != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"已缓存数据: {_cachedAppearanceData.ToBytes().Length} bytes", GUILayout.Width(200));
                if (GUILayout.Button("复制 Base64", GUILayout.Width(100)))
                {
                    CopyToClipboard();
                }
                GUILayout.EndHorizontal();
            }

            // 显示外观信息
            if (!string.IsNullOrEmpty(_appearanceInfo))
            {
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(400));
                GUILayout.TextArea(_appearanceInfo, GUILayout.ExpandHeight(true));
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }

        public void Update()
        {
            if (!IsEnabled || !_autoRefresh) return;

            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= REFRESH_INTERVAL)
            {
                _refreshTimer = 0f;
                GetLocalPlayerAppearance();
            }
        }

        /// <summary>
        /// 获取本地玩家外观数据
        /// </summary>
        private void GetLocalPlayerAppearance()
        {
            try
            {
                _appearanceInfo = "正在获取外观数据...\n";

                // 使用 AppearanceConverter 获取
                _cachedAppearanceData = AppearanceConverter.LoadMainCharacterAppearance();
                
                if (_cachedAppearanceData != null)
                {
                    _appearanceInfo = "✅ 成功获取外观数据\n";
                    _appearanceInfo += $"数据大小: {_cachedAppearanceData.ToBytes().Length} bytes\n";
                    _appearanceInfo += $"部位数量: {_cachedAppearanceData.Parts.Length}\n\n";
                    
                    // 显示简要信息
                    string[] partNames = { "发型", "眼睛", "眉毛", "嘴巴", "尾巴", "脚", "翅膀" };
                    foreach (var part in _cachedAppearanceData.Parts)
                    {
                        string name = part.PartType < partNames.Length ? partNames[part.PartType] : $"部位{part.PartType}";
                        _appearanceInfo += $"- {name}: ID={part.PartId}\n";
                    }
                    return;
                }

                _appearanceInfo = "❌ 无法获取外观数据\n";
                _appearanceInfo += "- CharacterMainControl.Main 可能为空\n";
            }
            catch (Exception ex)
            {
                _appearanceInfo = $"❌ 获取失败: {ex.Message}\n";
                Debug.LogError($"[CharacterAppearanceDebugModule] 异常: {ex}");
            }
        }

        /// <summary>
        /// 应用外观到本地玩家
        /// </summary>
        private void ApplyToLocalPlayer()
        {
            try
            {
                if (_cachedAppearanceData == null)
                {
                    _appearanceInfo += "\n❌ 请先获取外观数据\n";
                    return;
                }

                var character = CharacterMainControl.Main;
                if (character == null)
                {
                    _appearanceInfo += "\n❌ CharacterMainControl.Main 为空\n";
                    return;
                }

                bool success = AppearanceConverter.ApplyAppearanceToCharacter(character, _cachedAppearanceData);
                if (success)
                {
                    _appearanceInfo += "\n✅ 成功应用外观！\n";
                }
                else
                {
                    _appearanceInfo += "\n❌ 应用失败，查看日志\n";
                }
            }
            catch (Exception ex)
            {
                _appearanceInfo += $"\n❌ 应用失败: {ex.Message}\n";
                Debug.LogError($"[CharacterAppearanceDebugModule] 异常: {ex}");
            }
        }

        /// <summary>
        /// 复制到剪贴板
        /// </summary>
        private void CopyToClipboard()
        {
            try
            {
                if (_cachedAppearanceData == null)
                {
                    _appearanceInfo += "\n❌ 没有数据\n";
                    return;
                }

                byte[] bytes = _cachedAppearanceData.ToBytes();
                string base64 = Convert.ToBase64String(bytes);
                GUIUtility.systemCopyBuffer = base64;
                
                _appearanceInfo += $"\n✅ 已复制 (Base64, {base64.Length} 字符)\n";
            }
            catch (Exception ex)
            {
                _appearanceInfo += $"\n❌ 复制失败: {ex.Message}\n";
            }
        }

        public void OnDestroy()
        {
            // 清理资源
        }
    }
}
