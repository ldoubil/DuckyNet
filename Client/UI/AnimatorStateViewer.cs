using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.UI
{
    /// <summary>
    /// 动画状态机可视化窗口 - 实时显示玩家动画状态
    /// </summary>
    public class AnimatorStateViewer : IUIWindow
    {
        private Rect _windowRect = new Rect(50, 100, 500, 700);
        private bool _isVisible = false;
        private Vector2 _scrollPosition;
        
        // 样式
        private GUIStyle? _headerStyle;
        private GUIStyle? _stateStyle;
        private GUIStyle? _paramStyle;
        private GUIStyle? _activeStateStyle;

        // 缓存的动画信息
        private Animator? _playerAnimator;
        private float _updateInterval = 0.1f; // 更新间隔
        private float _lastUpdateTime = 0f;

        // 状态信息
        private List<LayerStateInfo> _layerStates = new List<LayerStateInfo>();
        private Dictionary<string, AnimatorControllerParameter> _parameters = new Dictionary<string, AnimatorControllerParameter>();

        public bool IsVisible => _isVisible;

        public void Toggle()
        {
            _isVisible = !_isVisible;
            
            if (_isVisible)
            {
                FindPlayerAnimator();
            }
        }

        public void Show()
        {
            _isVisible = true;
            FindPlayerAnimator();
        }

        public void Hide()
        {
            _isVisible = false;
        }

        public void Update()
        {
            if (!_isVisible) return;

            // 定期更新状态信息
            if (Time.time - _lastUpdateTime > _updateInterval)
            {
                UpdateAnimatorState();
                _lastUpdateTime = Time.time;
            }
        }

        public void OnGUI()
        {
            if (!_isVisible) return;

            try
            {
                InitStyles();
                _windowRect = GUILayout.Window(
                    GUIUtility.GetControlID(FocusType.Passive),
                    _windowRect,
                    DrawWindow,
                    "动画状态机可视化",
                    GUILayout.MinWidth(500),
                    GUILayout.MinHeight(700)
                );
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[AnimatorStateViewer] OnGUI 错误: {ex.Message}");
            }
        }

        public void Dispose()
        {
        }

        private void InitStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.cyan }
                };
            }

            if (_stateStyle == null)
            {
                _stateStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(10, 10, 5, 5),
                    normal = { textColor = Color.white }
                };
            }

            if (_activeStateStyle == null)
            {
                _activeStateStyle = new GUIStyle(_stateStyle)
                {
                    normal = { textColor = Color.green },
                    fontStyle = FontStyle.Bold
                };
            }

            if (_paramStyle == null)
            {
                _paramStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    normal = { textColor = Color.white }
                };
            }
        }

        private void DrawWindow(int windowID)
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            // 标题和刷新按钮
            GUILayout.BeginHorizontal();
            GUILayout.Label("🎬 玩家动画状态机", _headerStyle);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("🔄 刷新", GUILayout.Width(60), GUILayout.Height(25)))
            {
                FindPlayerAnimator();
                UpdateAnimatorState();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (_playerAnimator == null)
            {
                GUILayout.Label("❌ 未找到玩家 Animator", _headerStyle);
                GUILayout.Space(5);
                if (GUILayout.Button("🔍 查找玩家", GUILayout.Height(30)))
                {
                    FindPlayerAnimator();
                }
            }
            else
            {
                // Animator 基本信息
                DrawAnimatorInfo();
                
                GUILayout.Space(10);
                
                // 动画参数
                DrawParameters();
                
                GUILayout.Space(10);
                
                // 动画层状态
                DrawLayerStates();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private void DrawAnimatorInfo()
        {
            if (_playerAnimator == null) return;

            GUILayout.Label("=== Animator 信息 ===", _headerStyle);
            
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.2f, 0.3f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = oldColor;

            GUILayout.Label($"控制器: {_playerAnimator.runtimeAnimatorController?.name ?? "null"}");
            GUILayout.Label($"层数: {_playerAnimator.layerCount}");
            GUILayout.Label($"参数数: {_playerAnimator.parameterCount}");
            GUILayout.Label($"启用: {_playerAnimator.enabled}");
            GUILayout.Label($"更新模式: {_playerAnimator.updateMode}");
            GUILayout.Label($"速度: {_playerAnimator.speed:F2}");

            GUILayout.EndVertical();
        }

        private void DrawParameters()
        {
            if (_playerAnimator == null || _parameters.Count == 0) return;

            GUILayout.Label("=== 动画参数 ===", _headerStyle);

            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.3f, 0.2f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = oldColor;

            // 按类型分组显示
            var floatParams = _parameters.Values.Where(p => p.type == AnimatorControllerParameterType.Float).ToList();
            var intParams = _parameters.Values.Where(p => p.type == AnimatorControllerParameterType.Int).ToList();
            var boolParams = _parameters.Values.Where(p => p.type == AnimatorControllerParameterType.Bool).ToList();
            var triggerParams = _parameters.Values.Where(p => p.type == AnimatorControllerParameterType.Trigger).ToList();

            // Float 参数
            if (floatParams.Count > 0)
            {
                GUILayout.Label("📊 Float 参数:", _paramStyle);
                foreach (var param in floatParams)
                {
                    float value = _playerAnimator.GetFloat(param.nameHash);
                    DrawParameterBar(param.name, value, -2f, 2f);
                }
                GUILayout.Space(5);
            }

            // Int 参数
            if (intParams.Count > 0)
            {
                GUILayout.Label("🔢 Int 参数:", _paramStyle);
                foreach (var param in intParams)
                {
                    int value = _playerAnimator.GetInteger(param.nameHash);
                    GUILayout.Label($"  • {param.name} = {value}", _paramStyle);
                }
                GUILayout.Space(5);
            }

            // Bool 参数
            if (boolParams.Count > 0)
            {
                GUILayout.Label("✓ Bool 参数:", _paramStyle);
                foreach (var param in boolParams)
                {
                    bool value = _playerAnimator.GetBool(param.nameHash);
                    var color = value ? Color.green : Color.gray;
                    var oldContentColor = GUI.contentColor;
                    GUI.contentColor = color;
                    GUILayout.Label($"  • {param.name} = {(value ? "True" : "False")}", _paramStyle);
                    GUI.contentColor = oldContentColor;
                }
                GUILayout.Space(5);
            }

            // Trigger 参数
            if (triggerParams.Count > 0)
            {
                GUILayout.Label("⚡ Trigger 参数:", _paramStyle);
                foreach (var param in triggerParams)
                {
                    GUILayout.Label($"  • {param.name}", _paramStyle);
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawParameterBar(string name, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            
            GUILayout.Label($"  • {name}:", GUILayout.Width(120));
            
            // 进度条
            var rect = GUILayoutUtility.GetRect(200, 20);
            GUI.Box(rect, "");
            
            float normalizedValue = Mathf.InverseLerp(min, max, value);
            var barRect = new Rect(rect.x, rect.y, rect.width * normalizedValue, rect.height);
            
            var barColor = new Color(0.3f, 0.7f, 0.3f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, barColor, 0, 0);
            
            GUILayout.Label($"{value:F2}", GUILayout.Width(50));
            
            GUILayout.EndHorizontal();
        }

        private void DrawLayerStates()
        {
            if (_playerAnimator == null || _layerStates.Count == 0) return;

            GUILayout.Label("=== 动画层状态 ===", _headerStyle);

            foreach (var layerInfo in _layerStates)
            {
                DrawLayerState(layerInfo);
                GUILayout.Space(5);
            }
        }

        private void DrawLayerState(LayerStateInfo layerInfo)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.2f, 0.2f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = oldColor;

            // 层名称和权重
            GUILayout.BeginHorizontal();
            GUILayout.Label($"🎭 Layer {layerInfo.LayerIndex}: {layerInfo.LayerName}", _headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"权重: {layerInfo.Weight:F2}", _paramStyle);
            GUILayout.EndHorizontal();

            // 当前状态
            if (layerInfo.CurrentState != null)
            {
                DrawStateInfo("▶️ 当前状态", layerInfo.CurrentState, true);
            }

            // 过渡信息
            if (layerInfo.IsInTransition && layerInfo.NextState != null)
            {
                GUILayout.Space(5);
                
                // 过渡进度条
                GUILayout.Label($"🔄 过渡中... ({layerInfo.TransitionProgress * 100:F0}%)", _paramStyle);
                DrawProgressBar(layerInfo.TransitionProgress);
                
                GUILayout.Space(3);
                DrawStateInfo("⏭️ 目标状态", layerInfo.NextState, false);
            }

            GUILayout.EndVertical();
        }

        private void DrawStateInfo(string label, StateInfo stateInfo, bool isCurrent)
        {
            var style = isCurrent ? _activeStateStyle : _stateStyle;
            var color = isCurrent ? Color.green : Color.yellow;

            var oldContentColor = GUI.contentColor;
            GUI.contentColor = color;

            GUILayout.Label(label, _paramStyle);
            GUI.contentColor = oldContentColor;

            GUILayout.BeginVertical(style);
            GUILayout.Label($"状态: {stateInfo.Name}");
            GUILayout.Label($"标签: {string.Join(", ", stateInfo.Tags)}");
            GUILayout.Label($"播放时间: {stateInfo.NormalizedTime:F2} ({stateInfo.NormalizedTime * 100:F0}%)");
            GUILayout.Label($"速度: {stateInfo.Speed:F2}x");
            GUILayout.Label($"循环: {(stateInfo.IsLooping ? "是" : "否")}");
            
            // 播放进度条
            if (isCurrent)
            {
                DrawProgressBar(stateInfo.NormalizedTime % 1.0f);
            }
            
            GUILayout.EndVertical();
        }

        private void DrawProgressBar(float progress)
        {
            var rect = GUILayoutUtility.GetRect(GUILayoutUtility.GetLastRect().width, 15);
            GUI.Box(rect, "");
            
            float clampedProgress = Mathf.Clamp01(progress);
            var barRect = new Rect(rect.x, rect.y, rect.width * clampedProgress, rect.height);
            
            var barColor = new Color(0.2f, 0.6f, 0.9f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, barColor, 0, 0);
            
            // 百分比文字
            var oldColor = GUI.color;
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(rect, $"{clampedProgress * 100:F0}%", style);
            GUI.color = oldColor;
        }

        private void FindPlayerAnimator()
        {
            try
            {
                // 方法1: 通过 CharacterCustomizationManager 获取本地玩家角色
                var customizationManager = GameContext.Instance?.CharacterCustomizationManager;
                if (customizationManager != null)
                {
                    var getCharacterMethod = customizationManager.GetType().GetMethod("GetLocalPlayerCharacter",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (getCharacterMethod != null)
                    {
                        var character = getCharacterMethod.Invoke(customizationManager, null) as GameObject;
                        if (character != null)
                        {
                            UnityEngine.Debug.Log($"[AnimatorStateViewer] 从 CharacterCustomizationManager 获取角色: {character.name}");
                            _playerAnimator = character.GetComponentInChildren<Animator>();
                            
                            if (_playerAnimator != null)
                            {
                                InitializeAnimator(character);
                                return;
                            }
                        }
                    }
                }

                // 方法2: 查找场景中带有 CharacterMainControl 的对象（排除测试单位）
                UnityEngine.Debug.Log("[AnimatorStateViewer] 尝试从场景查找玩家角色...");
                var characterMainControlType = HarmonyLib.AccessTools.TypeByName("CharacterMainControl");
                
                if (characterMainControlType != null)
                {
                    var allCharacters = GameObject.FindObjectsOfType(characterMainControlType);
                    UnityEngine.Debug.Log($"[AnimatorStateViewer] 找到 {allCharacters.Length} 个角色");
                    
                    foreach (var characterControl in allCharacters)
                    {
                        var character = (characterControl as Component)?.gameObject;
                        if (character != null)
                        {
                            // 排除测试单位（名称包含 "Custom" 或 "Test"）
                            if (character.name.Contains("Custom") || character.name.Contains("Test"))
                            {
                                UnityEngine.Debug.Log($"[AnimatorStateViewer] 跳过测试单位: {character.name}");
                                continue;
                            }
                            
                            UnityEngine.Debug.Log($"[AnimatorStateViewer] 尝试角色: {character.name}");
                            
                            _playerAnimator = character.GetComponentInChildren<Animator>();
                            if (_playerAnimator != null)
                            {
                                InitializeAnimator(character);
                                return;
                            }
                        }
                    }
                }

                // 方法3: 查找所有 Animator，选择第一个有控制器的（排除测试单位）
                UnityEngine.Debug.Log("[AnimatorStateViewer] 尝试查找所有 Animator...");
                var allAnimators = GameObject.FindObjectsOfType<Animator>();
                UnityEngine.Debug.Log($"[AnimatorStateViewer] 找到 {allAnimators.Length} 个 Animator");
                
                foreach (var animator in allAnimators)
                {
                    if (animator.runtimeAnimatorController != null && 
                        animator.runtimeAnimatorController.name.Contains("Character"))
                    {
                        var character = animator.transform.root.gameObject;
                        
                        // 排除测试单位
                        if (character.name.Contains("Custom") || character.name.Contains("Test"))
                        {
                            continue;
                        }
                        
                        UnityEngine.Debug.Log($"[AnimatorStateViewer] 找到可能的玩家角色: {character.name}");
                        _playerAnimator = animator;
                        InitializeAnimator(character);
                        return;
                    }
                }

                UnityEngine.Debug.LogWarning("[AnimatorStateViewer] ❌ 未找到玩家角色的 Animator");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[AnimatorStateViewer] 查找 Animator 失败: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
            }
        }

        private void InitializeAnimator(GameObject character)
        {
            UnityEngine.Debug.Log($"[AnimatorStateViewer] ✅ 找到玩家 Animator: {character.name}");
            UnityEngine.Debug.Log($"   控制器: {_playerAnimator?.runtimeAnimatorController?.name}");
            UnityEngine.Debug.Log($"   层数: {_playerAnimator?.layerCount}");
            
            // 初始化参数字典
            _parameters.Clear();
            if (_playerAnimator != null)
            {
                foreach (var param in _playerAnimator.parameters)
                {
                    _parameters[param.name] = param;
                }
                
                UpdateAnimatorState();
            }
        }

        private void UpdateAnimatorState()
        {
            if (_playerAnimator == null) return;

            try
            {
                _layerStates.Clear();

                for (int i = 0; i < _playerAnimator.layerCount; i++)
                {
                    var layerInfo = new LayerStateInfo
                    {
                        LayerIndex = i,
                        LayerName = _playerAnimator.GetLayerName(i),
                        Weight = _playerAnimator.GetLayerWeight(i)
                    };

                    // 当前状态
                    var currentStateInfo = _playerAnimator.GetCurrentAnimatorStateInfo(i);
                    layerInfo.CurrentState = new StateInfo
                    {
                        Name = GetStateName(currentStateInfo.fullPathHash),
                        NameHash = currentStateInfo.fullPathHash,
                        NormalizedTime = currentStateInfo.normalizedTime,
                        Speed = currentStateInfo.speed,
                        IsLooping = currentStateInfo.loop,
                        Tags = GetStateTags(i, currentStateInfo)
                    };

                    // 检查是否在过渡中
                    if (_playerAnimator.IsInTransition(i))
                    {
                        layerInfo.IsInTransition = true;
                        
                        var transitionInfo = _playerAnimator.GetAnimatorTransitionInfo(i);
                        layerInfo.TransitionProgress = transitionInfo.normalizedTime;

                        var nextStateInfo = _playerAnimator.GetNextAnimatorStateInfo(i);
                        layerInfo.NextState = new StateInfo
                        {
                            Name = GetStateName(nextStateInfo.fullPathHash),
                            NameHash = nextStateInfo.fullPathHash,
                            NormalizedTime = nextStateInfo.normalizedTime,
                            Speed = nextStateInfo.speed,
                            IsLooping = nextStateInfo.loop,
                            Tags = GetStateTags(i, nextStateInfo)
                        };
                    }

                    _layerStates.Add(layerInfo);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[AnimatorStateViewer] 更新状态失败: {ex.Message}");
            }
        }

        private string GetStateName(int stateHash)
        {
            // 尝试从 Animator 获取状态名称
            // 如果无法获取，返回哈希值
            return $"State_{stateHash}";
        }

        private List<string> GetStateTags(int layerIndex, AnimatorStateInfo stateInfo)
        {
            var tags = new List<string>();
            
            try
            {
                // Unity 的 AnimatorStateInfo 可以检查标签
                // 常见的标签
                string[] commonTags = { "Attack", "Move", "Idle", "Dash", "Death", "Reload" };
                
                foreach (var tag in commonTags)
                {
                    if (_playerAnimator != null && _playerAnimator.GetCurrentAnimatorStateInfo(layerIndex).IsTag(tag))
                    {
                        tags.Add(tag);
                    }
                }
            }
            catch { }

            return tags;
        }

        // 数据结构
        private class LayerStateInfo
        {
            public int LayerIndex { get; set; }
            public string LayerName { get; set; } = "";
            public float Weight { get; set; }
            public StateInfo? CurrentState { get; set; }
            public bool IsInTransition { get; set; }
            public StateInfo? NextState { get; set; }
            public float TransitionProgress { get; set; }
        }

        private class StateInfo
        {
            public string Name { get; set; } = "";
            public int NameHash { get; set; }
            public float NormalizedTime { get; set; }
            public float Speed { get; set; }
            public bool IsLooping { get; set; }
            public List<string> Tags { get; set; } = new List<string>();
        }
    }
}

