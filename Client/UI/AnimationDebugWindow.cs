using System;
using System.Linq;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.UI
{
    /// <summary>
    /// 动画调试窗口 - 用于测试和查看单位动画
    /// </summary>
    public class AnimationDebugWindow : IUIWindow
    {
        private Rect _windowRect = new Rect(600, 100, 450, 600);
        private bool _isVisible = false;
        private Vector2 _scrollPosition;
        private GameObject? _selectedUnit;
        private int _selectedUnitIndex = 0;

        // 动画参数
        private float _moveSpeed = 1.0f;
        private float _moveDirX = 0f;
        private float _moveDirY = 1f;
        private bool _weaponOut = false;
        private int _handState = 0;
        private bool _dashing = false;

        // 层权重
        private string _layerName = "MeleeAttack";
        private float _layerWeight = 0f;

        // 自动测试
        private bool _autoTest = false;
        private float _autoTestTimer = 0f;
        private int _autoTestStep = 0;

        // 样式
        private GUIStyle? _headerStyle;
        private GUIStyle? _labelStyle;

        // 动画调试器
        private AnimationDebugger? _animationDebugger;

        public bool IsVisible => _isVisible;

        public AnimationDebugWindow()
        {
            _animationDebugger = new Core.Debug.AnimationDebugger();
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
            UnityEngine.Debug.Log($"[AnimationDebugWindow] 窗口切换: {(_isVisible ? "显示" : "隐藏")}");
        }

        public void Show()
        {
            _isVisible = true;
            UnityEngine.Debug.Log("[AnimationDebugWindow] 窗口显示");
        }

        public void Hide()
        {
            _isVisible = false;
            UnityEngine.Debug.Log("[AnimationDebugWindow] 窗口隐藏");
        }

        private void InitStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.yellow }
                };
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    normal = { textColor = Color.gray }
                };
            }
        }

        public void OnGUI()
        {
            if (!_isVisible) return;

            try
            {
                InitStyles();
                _windowRect = GUILayout.Window(1004, _windowRect, DrawWindow, "动画调试工具");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[AnimationDebugWindow] OnGUI 错误: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
            }
        }

        private void DrawWindow(int windowId)
        {
            if (!GameContext.IsInitialized)
            {
                GUILayout.Label("GameContext 未初始化");
                return;
            }

            var unitManager = GameContext.Instance.UnitManager;
            var units = unitManager.ManagedUnits;

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            // 单位选择
            GUILayout.Label("=== 单位选择 ===", _headerStyle);
            if (units.Count == 0)
            {
                GUILayout.Label("没有可用单位（使用调试窗口创建单位）");
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"选择单位 ({units.Count} 个):", GUILayout.Width(120));
                
                string[] unitNames = units.Select((u, i) => $"[{i}] {u.name}").ToArray();
                _selectedUnitIndex = GUILayout.SelectionGrid(_selectedUnitIndex, unitNames, 2);
                _selectedUnit = (_selectedUnitIndex >= 0 && _selectedUnitIndex < units.Count) 
                    ? units[_selectedUnitIndex] : null;
                
                GUILayout.EndHorizontal();

                if (_selectedUnit != null)
                {
                    GUILayout.Label($"当前选择: {_selectedUnit.name}", _labelStyle);
                }
            }

            GUILayout.Space(10);

            // 动画信息查看
            GUILayout.Label("=== 动画信息 ===", _headerStyle);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("查看当前单位", GUILayout.Height(30)))
            {
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.LogAnimationInfo(_selectedUnit);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("请先选择一个单位");
                }
            }

            if (GUILayout.Button("查看所有单位", GUILayout.Height(30)))
            {
                if (_animationDebugger != null)
                {
                    foreach (var unit in units)
                    {
                        if (unit != null)
                        {
                            _animationDebugger.LogAnimationInfo(unit);
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 诊断本地玩家", GUILayout.Height(30)))
            {
                if (_animationDebugger != null)
                {
                    _animationDebugger.DiagnoseLocalPlayerCharacter();
                }
            }

            if (GUILayout.Button("🔧 修复 Animator", GUILayout.Height(30)))
            {
                if (_selectedUnit != null)
                {
                    Core.Debug.AnimatorFixer.DiagnoseAndFix(_selectedUnit);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("请先选择一个单位");
                }
            }
            GUILayout.EndHorizontal();

            // 手动控制模式切换
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            
            bool isControlEnabled = _selectedUnit != null && Core.Debug.AnimatorFixer.IsAnimationControlEnabled(_selectedUnit);
            string buttonText = isControlEnabled ? "🎮 切换到手动控制" : "🤖 恢复自动控制";
            var buttonColor = isControlEnabled ? Color.yellow : Color.green;
            
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = buttonColor;
            
            if (GUILayout.Button(buttonText, GUILayout.Height(35)))
            {
                if (_selectedUnit != null)
                {
                    if (isControlEnabled)
                    {
                        // 切换到手动模式
                        if (Core.Debug.AnimatorFixer.DisableAnimationControl(_selectedUnit))
                        {
                            UnityEngine.Debug.Log("✅ 已切换到手动控制模式！现在设置的参数不会被覆盖了");
                        }
                    }
                    else
                    {
                        // 恢复自动模式
                        if (Core.Debug.AnimatorFixer.EnableAnimationControl(_selectedUnit))
                        {
                            UnityEngine.Debug.Log("✅ 已恢复自动控制模式");
                        }
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("请先选择一个单位");
                }
            }
            
            GUI.backgroundColor = oldColor;
            GUILayout.EndHorizontal();
            
            // 显示当前模式
            if (_selectedUnit != null)
            {
                string modeText = isControlEnabled ? "⚠️ 当前: 自动控制模式（游戏脚本控制）" : "✅ 当前: 手动控制模式（可调试动画）";
                var modeColor = isControlEnabled ? Color.red : Color.green;
                
                var oldTextColor = GUI.contentColor;
                GUI.contentColor = modeColor;
                GUILayout.Label(modeText, _headerStyle);
                GUI.contentColor = oldTextColor;
            }

            GUILayout.Space(10);

            // 移动动画控制
            GUILayout.Label("=== 移动动画 ===", _headerStyle);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("移动速度:", GUILayout.Width(80));
            _moveSpeed = GUILayout.HorizontalSlider(_moveSpeed, 0f, 2f, GUILayout.Width(150));
            GUILayout.Label($"{_moveSpeed:F2}", GUILayout.Width(50));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("方向 X:", GUILayout.Width(80));
            _moveDirX = GUILayout.HorizontalSlider(_moveDirX, -1f, 1f, GUILayout.Width(150));
            GUILayout.Label($"{_moveDirX:F2}", GUILayout.Width(50));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("方向 Y:", GUILayout.Width(80));
            _moveDirY = GUILayout.HorizontalSlider(_moveDirY, -1f, 1f, GUILayout.Width(150));
            GUILayout.Label($"{_moveDirY:F2}", GUILayout.Width(50));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("应用移动", GUILayout.Height(30)))
            {
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.SetAnimatorFloat(_selectedUnit, "MoveSpeed", _moveSpeed);
                    _animationDebugger.SetAnimatorFloat(_selectedUnit, "MoveDirX", _moveDirX);
                    _animationDebugger.SetAnimatorFloat(_selectedUnit, "MoveDirY", _moveDirY);
                }
            }

            if (GUILayout.Button("停止移动", GUILayout.Height(30)))
            {
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.SetAnimatorFloat(_selectedUnit, "MoveSpeed", 0);
                    _animationDebugger.SetAnimatorFloat(_selectedUnit, "MoveDirX", 0);
                    _animationDebugger.SetAnimatorFloat(_selectedUnit, "MoveDirY", 0);
                }
            }
            GUILayout.EndHorizontal();

            // 快捷移动按钮
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("向前", GUILayout.Height(25)))
            {
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.TestMovementAnimation(_selectedUnit, _moveSpeed, new Vector2(0, 1));
                }
            }
            if (GUILayout.Button("向后", GUILayout.Height(25)))
            {
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.TestMovementAnimation(_selectedUnit, _moveSpeed, new Vector2(0, -1));
                }
            }
            if (GUILayout.Button("向左", GUILayout.Height(25)))
            {
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.TestMovementAnimation(_selectedUnit, _moveSpeed, new Vector2(-1, 0));
                }
            }
            if (GUILayout.Button("向右", GUILayout.Height(25)))
            {
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.TestMovementAnimation(_selectedUnit, _moveSpeed, new Vector2(1, 0));
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 攻击和状态动画
            GUILayout.Label("=== 攻击与状态 ===", _headerStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("触发攻击", GUILayout.Height(30)))
            {
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.TriggerAnimation(_selectedUnit, "Attack");
                }
            }

            if (GUILayout.Button("全体攻击", GUILayout.Height(30)))
            {
                if (_animationDebugger != null)
                {
                    foreach (var unit in units)
                    {
                        if (unit != null)
                        {
                            _animationDebugger.TriggerAnimation(unit, "Attack");
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("武器状态:", GUILayout.Width(80));
            bool newWeaponOut = GUILayout.Toggle(_weaponOut, "拿出武器");
            if (newWeaponOut != _weaponOut)
            {
                _weaponOut = newWeaponOut;
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.SetAnimatorBool(_selectedUnit, "RightHandOut", _weaponOut);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("手部状态:", GUILayout.Width(80));
            int newHandState = (int)GUILayout.HorizontalSlider(_handState, 0, 5);
            if (newHandState != _handState)
            {
                _handState = newHandState;
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.SetAnimatorInt(_selectedUnit, "HandState", _handState);
                }
            }
            GUILayout.Label($"{_handState}", GUILayout.Width(50));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("冲刺状态:", GUILayout.Width(80));
            bool newDashing = GUILayout.Toggle(_dashing, "冲刺中");
            if (newDashing != _dashing)
            {
                _dashing = newDashing;
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.SetAnimatorBool(_selectedUnit, "Dashing", _dashing);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 动画层权重控制
            GUILayout.Label("=== 动画层权重 ===", _headerStyle);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("层名称:", GUILayout.Width(80));
            _layerName = GUILayout.TextField(_layerName, GUILayout.Width(150));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("权重:", GUILayout.Width(80));
            _layerWeight = GUILayout.HorizontalSlider(_layerWeight, 0f, 1f, GUILayout.Width(150));
            GUILayout.Label($"{_layerWeight:F2}", GUILayout.Width(50));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("应用层权重", GUILayout.Height(30)))
            {
                if (_selectedUnit != null && _animationDebugger != null)
                {
                    _animationDebugger.SetLayerWeight(_selectedUnit, _layerName, _layerWeight);
                }
            }

            GUILayout.Space(10);

            // 批量测试
            GUILayout.Label("=== 批量测试 ===", _headerStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("全体向前移动", GUILayout.Height(30)))
            {
                if (_animationDebugger != null)
                {
                    foreach (var unit in units)
                    {
                        if (unit != null)
                        {
                            _animationDebugger.TestMovementAnimation(unit, _moveSpeed, new Vector2(0, 1));
                        }
                    }
                }
            }

            if (GUILayout.Button("全体停止", GUILayout.Height(30)))
            {
                if (_animationDebugger != null)
                {
                    foreach (var unit in units)
                    {
                        if (unit != null)
                        {
                            _animationDebugger.SetAnimatorFloat(unit, "MoveSpeed", 0);
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 自动测试
            GUILayout.Label("=== 自动测试 ===", _headerStyle);
            
            GUILayout.BeginHorizontal();
            _autoTest = GUILayout.Toggle(_autoTest, "自动测试动画循环");
            if (_autoTest)
            {
                GUILayout.Label($"步骤: {_autoTestStep}", GUILayout.Width(100));
            }
            GUILayout.EndHorizontal();

            if (_autoTest && _selectedUnit != null)
            {
                GUILayout.Label("(自动循环: 站立 → 前进 → 攻击 → 后退)", _labelStyle);
            }

            GUILayout.EndScrollView();

            // 拖动窗口
            GUI.DragWindow();
        }

        public void Update()
        {
            // 自动测试逻辑
            if (_autoTest && _selectedUnit != null && _animationDebugger != null)
            {
                _autoTestTimer += Time.deltaTime;

                if (_autoTestTimer >= 2.0f) // 每2秒切换一次动作
                {
                    _autoTestTimer = 0f;

                    switch (_autoTestStep)
                    {
                        case 0: // 站立
                            _animationDebugger.SetAnimatorFloat(_selectedUnit, "MoveSpeed", 0);
                            break;
                        case 1: // 前进
                            _animationDebugger.TestMovementAnimation(_selectedUnit, 1.0f, new Vector2(0, 1));
                            break;
                        case 2: // 攻击
                            _animationDebugger.TriggerAnimation(_selectedUnit, "Attack");
                            break;
                        case 3: // 后退
                            _animationDebugger.TestMovementAnimation(_selectedUnit, 1.0f, new Vector2(0, -1));
                            break;
                    }

                    _autoTestStep = (_autoTestStep + 1) % 4;
                }
            }
            else
            {
                _autoTestTimer = 0f;
                _autoTestStep = 0;
            }
        }

        public void Dispose()
        {
            // 清理资源
        }
    }
}

