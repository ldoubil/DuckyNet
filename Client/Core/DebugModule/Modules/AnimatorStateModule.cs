using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 动画状态机模块 - 从 AnimatorStateViewer 转换而来
    /// </summary>
    public class AnimatorStateModule : IDebugModule
    {
        private Animator? _playerAnimator;
        private float _updateInterval = 0.1f;
        private float _lastUpdateTime = 0f;

        private List<LayerStateInfo> _layerStates = new List<LayerStateInfo>();
        private Dictionary<string, AnimatorControllerParameter> _parameters = new Dictionary<string, AnimatorControllerParameter>();

        public string ModuleName => "状态机可视化";
        public string Category => "动画";
        public string Description => "实时显示玩家动画状态和参数";
        public bool IsEnabled { get; set; } = true;

        public AnimatorStateModule()
        {
        }

        public void OnGUI()
        {
            if (!GameContext.IsInitialized)
            {
                GUILayout.Label("游戏上下文未初始化", GUI.skin.label);
                return;
            }

            GUILayout.BeginVertical();

            if (GUILayout.Button("查找玩家 Animator"))
            {
                FindPlayerAnimator();
            }

            if (_playerAnimator == null)
            {
                GUILayout.Label("未找到玩家 Animator", GUI.skin.label);
                GUILayout.EndVertical();
                return;
            }

            // Animator 基本信息
            GUILayout.Label("=== Animator 信息 ===", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.Label($"控制器: {_playerAnimator.runtimeAnimatorController?.name ?? "null"}");
            GUILayout.Label($"层数: {_playerAnimator.layerCount}");
            GUILayout.Label($"参数数: {_playerAnimator.parameterCount}");
            GUILayout.Label($"启用: {_playerAnimator.enabled}");

            GUILayout.Space(5);

            // 动画参数（简化显示）
            if (_parameters.Count > 0)
            {
                GUILayout.Label("=== 参数值 ===", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                
                // Float 参数
                var floatParams = _parameters.Values.Where(p => p.type == AnimatorControllerParameterType.Float).Take(5);
                foreach (var param in floatParams)
                {
                    float value = _playerAnimator.GetFloat(param.nameHash);
                    GUILayout.Label($"  {param.name}: {value:F2}");
                }

                // Bool 参数
                var boolParams = _parameters.Values.Where(p => p.type == AnimatorControllerParameterType.Bool).Take(5);
                foreach (var param in boolParams)
                {
                    bool value = _playerAnimator.GetBool(param.nameHash);
                    GUILayout.Label($"  {param.name}: {value}");
                }
            }

            GUILayout.Space(5);

            // 动画层状态（简化显示）
            if (_layerStates.Count > 0)
            {
                GUILayout.Label("=== 层状态 ===", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                
                foreach (var layerInfo in _layerStates)
                {
                    GUILayout.Label($"层 {layerInfo.LayerIndex}: {layerInfo.LayerName}");
                    GUILayout.Label($"  权重: {layerInfo.Weight:F2}");
                    
                    if (layerInfo.CurrentState != null)
                    {
                        GUILayout.Label($"  当前: {layerInfo.CurrentState.Name}");
                        GUILayout.Label($"  进度: {layerInfo.CurrentState.NormalizedTime * 100:F0}%");
                    }

                    if (layerInfo.IsInTransition && layerInfo.NextState != null)
                    {
                        GUILayout.Label($"  过渡中 → {layerInfo.NextState.Name}");
                        GUILayout.Label($"  过渡: {layerInfo.TransitionProgress * 100:F0}%");
                    }
                }
            }

            GUILayout.EndVertical();
        }

        public void Update()
        {
            if (!IsEnabled) return;

            // 定期更新状态信息
            if (Time.time - _lastUpdateTime > _updateInterval)
            {
                UpdateAnimatorState();
                _lastUpdateTime = Time.time;
            }
        }

        private void FindPlayerAnimator()
        {
            try
            {
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
                            _playerAnimator = character.GetComponentInChildren<Animator>();
                            if (_playerAnimator != null)
                            {
                                InitializeAnimator();
                                return;
                            }
                        }
                    }
                }

                // 备用方法：查找场景中的 Animator
                var allAnimators = GameObject.FindObjectsOfType<Animator>();
                foreach (var animator in allAnimators)
                {
                    if (animator.runtimeAnimatorController != null && 
                        animator.runtimeAnimatorController.name.Contains("Character"))
                    {
                        var character = animator.transform.root.gameObject;
                        if (!character.name.Contains("Custom") && !character.name.Contains("Test"))
                        {
                            _playerAnimator = animator;
                            InitializeAnimator();
                            return;
                        }
                    }
                }

                UnityEngine.Debug.LogWarning("[AnimatorStateModule] 未找到玩家角色的 Animator");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[AnimatorStateModule] 查找 Animator 失败: {ex.Message}");
            }
        }

        private void InitializeAnimator()
        {
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

                    var currentStateInfo = _playerAnimator.GetCurrentAnimatorStateInfo(i);
                    layerInfo.CurrentState = new StateInfo
                    {
                        Name = $"State_{currentStateInfo.fullPathHash}",
                        NameHash = currentStateInfo.fullPathHash,
                        NormalizedTime = currentStateInfo.normalizedTime,
                        Speed = currentStateInfo.speed,
                        IsLooping = currentStateInfo.loop
                    };

                    if (_playerAnimator.IsInTransition(i))
                    {
                        layerInfo.IsInTransition = true;
                        var transitionInfo = _playerAnimator.GetAnimatorTransitionInfo(i);
                        layerInfo.TransitionProgress = transitionInfo.normalizedTime;

                        var nextStateInfo = _playerAnimator.GetNextAnimatorStateInfo(i);
                        layerInfo.NextState = new StateInfo
                        {
                            Name = $"State_{nextStateInfo.fullPathHash}",
                            NameHash = nextStateInfo.fullPathHash,
                            NormalizedTime = nextStateInfo.normalizedTime,
                            Speed = nextStateInfo.speed,
                            IsLooping = nextStateInfo.loop
                        };
                    }

                    _layerStates.Add(layerInfo);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[AnimatorStateModule] 更新状态失败: {ex.Message}");
            }
        }

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
        }
    }
}
