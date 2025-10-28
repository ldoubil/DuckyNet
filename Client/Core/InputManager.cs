using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 输入管理器
    /// 统一管理所有按键输入和快捷键
    /// </summary>
    public class InputManager : IDisposable
    {
        private readonly Dictionary<KeyCode, Action> _keyBindings;
        private bool _isEnabled;

        public InputManager()
        {
            _keyBindings = new Dictionary<KeyCode, Action>();
            _isEnabled = true;
        }

        /// <summary>
        /// 启用/禁用输入管理器
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                Debug.Log($"[InputManager] 输入管理器已{(value ? "启用" : "禁用")}");
            }
        }

        /// <summary>
        /// 注册按键绑定
        /// </summary>
        public void RegisterKey(KeyCode key, Action action, string description = "")
        {
            if (_keyBindings.ContainsKey(key))
            {
                Debug.LogWarning($"[InputManager] 按键 {key} 已被注册，将被覆盖");
            }

            _keyBindings[key] = action;
            
            var desc = string.IsNullOrEmpty(description) ? "" : $" ({description})";
            Debug.Log($"[InputManager] 按键 {key} 已注册{desc}");
        }

        /// <summary>
        /// 取消按键绑定
        /// </summary>
        public void UnregisterKey(KeyCode key)
        {
            if (_keyBindings.Remove(key))
            {
                Debug.Log($"[InputManager] 按键 {key} 已取消注册");
            }
        }

        /// <summary>
        /// 清除所有按键绑定
        /// </summary>
        public void ClearAllBindings()
        {
            _keyBindings.Clear();
            Debug.Log("[InputManager] 所有按键绑定已清除");
        }

        /// <summary>
        /// 更新输入（每帧调用）
        /// </summary>
        public void Update()
        {
            if (!_isEnabled) return;

            try
            {
                foreach (var kvp in _keyBindings)
                {
                    if (Input.GetKeyDown(kvp.Key))
                    {
                        try
                        {
                            kvp.Value?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[InputManager] 执行按键 {kvp.Key} 的回调时出错: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputManager] 更新输入时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            ClearAllBindings();
            Debug.Log("[InputManager] 输入管理器已清理");
        }
    }
}

