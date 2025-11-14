using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckyNet.RPC;
using DuckyNet.RPC.Core;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.DebugModule;

namespace DuckyNet.Client.UI
{
    /// <summary>
    /// 调试窗口 - 模块化版本
    /// 自动发现并加载所有调试模块
    /// </summary>
    public class DebugWindow : IUIWindow
    {
        private readonly RpcClient _client;
        private readonly DebugModuleManager _moduleManager;
        
        private Rect _windowRect = new Rect(Screen.width - 520, 100, 500, 600);
        private bool _isVisible = false;
        private Vector2 _scrollPosition = Vector2.zero;
        
        // 分类折叠状态
        private Dictionary<string, bool> _categoryFoldouts = new Dictionary<string, bool>();
        
        // 当前选中的标签页
        private int _selectedTab = 0;
        private string[] _tabs = { "模块", "设置" };

        public bool IsVisible => _isVisible;

        public DebugWindow(RpcClient client)
        {
            _client = client;
            _moduleManager = new DebugModuleManager(client);
            
            // 自动发现并注册所有模块
            _moduleManager.DiscoverAndRegisterModules();
            
            // 初始化所有分类为展开状态
            foreach (var category in _moduleManager.GetAllCategories())
            {
                _categoryFoldouts[category] = true;
            }
            
            UnityEngine.Debug.Log($"[DebugWindow] 调试窗口初始化完成，共加载 {_moduleManager.AllModules.Count} 个模块");
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
        }

        public void Show()
        {
            _isVisible = true;
        }

        public void Hide()
        {
            _isVisible = false;
        }

        public void Update()
        {
            if (_isVisible)
            {
                _moduleManager.Update();
            }
        }

        public void OnGUI()
        {
            if (!_isVisible) return;

            _windowRect = GUILayout.Window(1002, _windowRect, DrawWindow, "DuckyNet 调试工具 v2.0");
        }

        private void DrawWindow(int windowId)
        {
            // 绘制标签页
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);
            
            GUILayout.Space(5);
            
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            
            switch (_selectedTab)
            {
                case 0:
                    DrawModulesTab();
                    break;
                case 1:
                    DrawSettingsTab();
                    break;
            }
            
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        /// <summary>
        /// 绘制模块标签页
        /// </summary>
        private void DrawModulesTab()
        {
            GUILayout.BeginVertical();

            // 按分类显示所有模块
            var categories = _moduleManager.GetAllCategories();
            
            if (categories.Count == 0)
            {
                GUILayout.Label("未找到任何调试模块", GetWarningStyle());
            }
            else
            {
                foreach (var category in categories)
                {
                    DrawCategoryModules(category);
                    GUILayout.Space(10);
                }
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制设置标签页
        /// </summary>
        private void DrawSettingsTab()
        {
            GUILayout.BeginVertical();

            GUILayout.Label("=== 模块设置 ===", GetHeaderStyle());
            GUILayout.Space(5);

            // 显示所有模块的开关
            foreach (var module in _moduleManager.AllModules)
            {
                GUILayout.BeginHorizontal();
                
                bool wasEnabled = module.IsEnabled;
                bool isEnabled = GUILayout.Toggle(wasEnabled, "");
                
                if (isEnabled != wasEnabled)
                {
                    module.IsEnabled = isEnabled;
                    UnityEngine.Debug.Log($"[DebugWindow] 模块 {module.ModuleName} {(isEnabled ? "启用" : "禁用")}");
                }
                
                GUILayout.Label($"{module.ModuleName} ({module.Category})");
                GUILayout.FlexibleSpace();
                
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            // 全局操作按钮
            GUILayout.Label("=== 全局操作 ===", GetHeaderStyle());
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("启用所有"))
            {
                foreach (var module in _moduleManager.AllModules)
                {
                    module.IsEnabled = true;
                }
            }
            if (GUILayout.Button("禁用所有"))
            {
                foreach (var module in _moduleManager.AllModules)
                {
                    module.IsEnabled = false;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 统计信息
            GUILayout.Label("=== 统计信息 ===", GetHeaderStyle());
            GUILayout.Label($"总模块数: {_moduleManager.AllModules.Count}");
            GUILayout.Label($"启用模块: {_moduleManager.AllModules.Count(m => m.IsEnabled)}");
            GUILayout.Label($"分类数量: {_moduleManager.GetAllCategories().Count}");

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制某个分类下的所有模块
        /// </summary>
        private void DrawCategoryModules(string category)
        {
            // 确保分类存在于字典中
            if (!_categoryFoldouts.ContainsKey(category))
            {
                _categoryFoldouts[category] = true;
            }

            // 绘制分类标题（可折叠）
            GUILayout.BeginHorizontal();
            _categoryFoldouts[category] = GUILayout.Toggle(
                _categoryFoldouts[category], 
                $"▼ {category}", 
                GetHeaderStyle()
            );
            GUILayout.EndHorizontal();

            // 如果展开，绘制模块内容
            if (_categoryFoldouts[category])
            {
                var modules = _moduleManager.GetModulesByCategory(category);
                
                foreach (var module in modules)
                {
                    if (!module.IsEnabled) continue;

                    try
                    {
                        GUILayout.BeginVertical(GUI.skin.box);
                        
                        // 模块标题
                        GUILayout.Label($"● {module.ModuleName}", GetSubHeaderStyle());
                        
                        if (!string.IsNullOrEmpty(module.Description))
                        {
                            var descStyle = new GUIStyle(GUI.skin.label);
                            descStyle.fontSize = 10;
                            descStyle.normal.textColor = Color.gray;
                            GUILayout.Label(module.Description, descStyle);
                        }
                        
                        GUILayout.Space(5);
                        
                        // 绘制模块UI
                        module.OnGUI();
                        
                        GUILayout.EndVertical();
                        GUILayout.Space(5);
                    }
                    catch (Exception ex)
                    {
                        GUILayout.Label($"模块错误: {ex.Message}", GetErrorStyle());
                        UnityEngine.Debug.LogError($"[DebugWindow] 模块 {module.ModuleName} 渲染失败: {ex}");
                    }
                }
            }
        }

        private GUIStyle GetHeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 13;
            return style;
        }

        private GUIStyle GetSubHeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 11;
            return style;
        }

        private GUIStyle GetWarningStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = Color.yellow;
            return style;
        }

        private GUIStyle GetErrorStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = Color.red;
            return style;
        }

        public void Dispose()
        {
            _moduleManager?.Dispose();
        }
    }
}
