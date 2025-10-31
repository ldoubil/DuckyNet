using System;
using System.Linq;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.DebugModule;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 动画控制调试模块 - 从 AnimationDebugWindow 转换而来
    /// </summary>
    public class AnimationControlModule : IDebugModule
    {
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

        // 动画调试器
        private AnimationDebugger? _animationDebugger;

        public string ModuleName => "动画控制";
        public string Category => "动画";
        public string Description => "控制单位动画参数和测试动画";
        public bool IsEnabled { get; set; } = true;

        public AnimationControlModule()
        {
            _animationDebugger = new AnimationDebugger();
        }

        public void OnGUI()
        {
            if (!GameContext.IsInitialized)
            {
                GUILayout.Label("游戏上下文未初始化", GUI.skin.label);
                return;
            }

            var unitManager = GameContext.Instance.UnitManager;
            var units = unitManager.ManagedRemotePlayers;

            GUILayout.BeginVertical();

            // 单位选择（简化版）
            if (units.Count == 0)
            {
                GUILayout.Label("没有可用单位", GUI.skin.label);
            }
            else
            {
                GUILayout.Label($"单位数量: {units.Count}");
                
                if (units.Count <= 5)
                {
                    // 如果单位少，显示单选按钮
                    for (int i = 0; i < units.Count; i++)
                    {
                        bool selected = _selectedUnitIndex == i;
                        bool newSelected = GUILayout.Toggle(selected, units[i].name);
                        if (newSelected != selected)
                        {
                            _selectedUnitIndex = i;
                            _selectedUnit = units[i];
                        }
                    }
                }
                else
                {
                    // 单位多时使用下拉
                    string[] unitNames = units.Select((u, i) => $"[{i}] {u.name}").ToArray();
                    _selectedUnitIndex = GUILayout.SelectionGrid(_selectedUnitIndex, unitNames, 2);
                    _selectedUnit = (_selectedUnitIndex >= 0 && _selectedUnitIndex < units.Count) 
                        ? units[_selectedUnitIndex] : null;
                }
            }

            if (_selectedUnit != null)
            {
                GUILayout.Space(5);

                // 快速操作按钮
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("查看信息"))
                {
                    _animationDebugger?.LogAnimationInfo(_selectedUnit);
                }
                if (GUILayout.Button("诊断"))
                {
                    _animationDebugger?.DiagnoseLocalPlayerCharacter();
                }
                if (GUILayout.Button("修复"))
                {
                    AnimatorFixer.DiagnoseAndFix(_selectedUnit);
                }
                GUILayout.EndHorizontal();

                // 控制模式切换
                bool isControlEnabled = AnimatorFixer.IsAnimationControlEnabled(_selectedUnit);
                string buttonText = isControlEnabled ? "切换到手动控制" : "恢复自动控制";
                
                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = isControlEnabled ? Color.yellow : Color.green;
                
                if (GUILayout.Button(buttonText))
                {
                    if (isControlEnabled)
                    {
                        AnimatorFixer.DisableAnimationControl(_selectedUnit);
                    }
                    else
                    {
                        AnimatorFixer.EnableAnimationControl(_selectedUnit);
                    }
                }
                GUI.backgroundColor = oldColor;

                GUILayout.Space(5);
                GUILayout.Label("=== 移动动画 ===", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                
                GUILayout.BeginHorizontal();
                GUILayout.Label("速度:", GUILayout.Width(50));
                _moveSpeed = GUILayout.HorizontalSlider(_moveSpeed, 0f, 2f);
                GUILayout.Label($"{_moveSpeed:F2}", GUILayout.Width(40));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("方向X:", GUILayout.Width(50));
                _moveDirX = GUILayout.HorizontalSlider(_moveDirX, -1f, 1f);
                GUILayout.Label($"{_moveDirX:F2}", GUILayout.Width(40));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("方向Y:", GUILayout.Width(50));
                _moveDirY = GUILayout.HorizontalSlider(_moveDirY, -1f, 1f);
                GUILayout.Label($"{_moveDirY:F2}", GUILayout.Width(40));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("应用移动"))
                {
                    _animationDebugger?.SetAnimatorFloat(_selectedUnit, "MoveSpeed", _moveSpeed);
                    _animationDebugger?.SetAnimatorFloat(_selectedUnit, "MoveDirX", _moveDirX);
                    _animationDebugger?.SetAnimatorFloat(_selectedUnit, "MoveDirY", _moveDirY);
                }
                if (GUILayout.Button("停止"))
                {
                    _animationDebugger?.SetAnimatorFloat(_selectedUnit, "MoveSpeed", 0);
                }
                GUILayout.EndHorizontal();

                // 快捷方向按钮
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("前")) _animationDebugger?.TestMovementAnimation(_selectedUnit, _moveSpeed, new Vector2(0, 1));
                if (GUILayout.Button("后")) _animationDebugger?.TestMovementAnimation(_selectedUnit, _moveSpeed, new Vector2(0, -1));
                if (GUILayout.Button("左")) _animationDebugger?.TestMovementAnimation(_selectedUnit, _moveSpeed, new Vector2(-1, 0));
                if (GUILayout.Button("右")) _animationDebugger?.TestMovementAnimation(_selectedUnit, _moveSpeed, new Vector2(1, 0));
                GUILayout.EndHorizontal();

                GUILayout.Space(5);
                GUILayout.Label("=== 攻击与状态 ===", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("触发攻击"))
                {
                    _animationDebugger?.TriggerAnimation(_selectedUnit, "Attack");
                }
                GUILayout.EndHorizontal();

                bool newWeaponOut = GUILayout.Toggle(_weaponOut, "武器拿出");
                if (newWeaponOut != _weaponOut)
                {
                    _weaponOut = newWeaponOut;
                    _animationDebugger?.SetAnimatorBool(_selectedUnit, "RightHandOut", _weaponOut);
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label("手部状态:", GUILayout.Width(70));
                int newHandState = (int)GUILayout.HorizontalSlider(_handState, 0, 5);
                GUILayout.Label($"{newHandState}", GUILayout.Width(30));
                if (newHandState != _handState)
                {
                    _handState = newHandState;
                    _animationDebugger?.SetAnimatorInt(_selectedUnit, "HandState", _handState);
                }
                GUILayout.EndHorizontal();

                bool newDashing = GUILayout.Toggle(_dashing, "冲刺");
                if (newDashing != _dashing)
                {
                    _dashing = newDashing;
                    _animationDebugger?.SetAnimatorBool(_selectedUnit, "Dashing", _dashing);
                }

                GUILayout.Space(5);
                GUILayout.Label("=== 层权重 ===", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                
                _layerName = GUILayout.TextField(_layerName);
                
                GUILayout.BeginHorizontal();
                GUILayout.Label("权重:", GUILayout.Width(50));
                _layerWeight = GUILayout.HorizontalSlider(_layerWeight, 0f, 1f);
                GUILayout.Label($"{_layerWeight:F2}", GUILayout.Width(40));
                if (GUILayout.Button("应用", GUILayout.Width(50)))
                {
                    _animationDebugger?.SetLayerWeight(_selectedUnit, _layerName, _layerWeight);
                }
                GUILayout.EndHorizontal();

                // 自动测试
                GUILayout.Space(5);
                _autoTest = GUILayout.Toggle(_autoTest, "自动测试动画循环");
                if (_autoTest)
                {
                    GUILayout.Label($"步骤: {_autoTestStep}");
                }
            }

            GUILayout.EndVertical();
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
    }
}
