using System;
using HarmonyLib;
using UnityEngine;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 屏蔽游戏输入的补丁
    /// 当聊天窗口打开输入框时，同时禁用 CharacterInputControl 和 UIInputManager
    /// </summary>
    [HarmonyPatch]
    public static class InputBlockingPatch
    {
        private static bool _shouldBlockGameInput = false;
        private static bool _wasCharacterInputEnabled = true;
        private static bool _wasUIInputManagerEnabled = true;
        private static bool _wasPlayerInputEnabled = true; // 记录 GameManager.MainPlayerInput 状态

        /// <summary>
        /// 是否应该屏蔽游戏输入
        /// </summary>
        public static bool ShouldBlockGameInput
        {
            get => _shouldBlockGameInput;
            set
            {
                if (_shouldBlockGameInput != value)
                {
                    _shouldBlockGameInput = value;
                    UpdateInputControlState();
                }
            }
        }

        /// <summary>
        /// 更新所有输入控制器的启用状态
        /// </summary>
        private static void UpdateInputControlState()
        {
            try
            {
                // 1. 禁用 CharacterInputControl（角色控制：WASD、射击等）
                var characterInput = CharacterInputControl.Instance;
                if (characterInput != null)
                {
                    if (_shouldBlockGameInput)
                    {
                        _wasCharacterInputEnabled = characterInput.enabled;
                        characterInput.enabled = false;
                        Debug.Log($"[InputBlockingPatch] 已禁用 CharacterInputControl (之前: {_wasCharacterInputEnabled})");
                    }
                    else
                    {
                        characterInput.enabled = _wasCharacterInputEnabled;
                        Debug.Log($"[InputBlockingPatch] 已恢复 CharacterInputControl (恢复到: {_wasCharacterInputEnabled})");
                    }
                }
                else
                {
                    Debug.LogWarning("[InputBlockingPatch] CharacterInputControl.Instance 为 null");
                }

                // 2. 禁用 UIInputManager（UI 控制：I、O、P、M 等）
                var uiInputManager = UIInputManager.Instance;
                if (uiInputManager != null)
                {
                    if (_shouldBlockGameInput)
                    {
                        _wasUIInputManagerEnabled = uiInputManager.enabled;
                        uiInputManager.enabled = false;
                        Debug.Log($"[InputBlockingPatch] 已禁用 UIInputManager (之前: {_wasUIInputManagerEnabled})");
                    }
                    else
                    {
                        uiInputManager.enabled = _wasUIInputManagerEnabled;
                        Debug.Log($"[InputBlockingPatch] 已恢复 UIInputManager (恢复到: {_wasUIInputManagerEnabled})");
                    }
                }
                else
                {
                    Debug.LogWarning("[InputBlockingPatch] UIInputManager.Instance 为 null");
                }

                // 3. 统一禁用 PlayerInput（最底层 InputAction 资产驱动，覆盖仍未捕获的 I/O/P/M 等动作）
                try
                {
                    var playerInput = GameManager.MainPlayerInput; // PlayerInput 是 Unity 输入系统入口
                    if (playerInput != null)
                    {
                        if (_shouldBlockGameInput)
                        {
                            _wasPlayerInputEnabled = playerInput.enabled;
                            if (playerInput.enabled)
                            {
                                playerInput.enabled = false;
                                Debug.Log("[InputBlockingPatch] 已禁用 PlayerInput (阻断所有 InputAction 事件)");
                            }
                        }
                        else
                        {
                            playerInput.enabled = _wasPlayerInputEnabled;
                            Debug.Log($"[InputBlockingPatch] 已恢复 PlayerInput (恢复到: {_wasPlayerInputEnabled})");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[InputBlockingPatch] GameManager.MainPlayerInput 为 null");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[InputBlockingPatch] 切换 PlayerInput 失败: {e.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputBlockingPatch] 更新输入状态失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Patch CharacterInputControl.Update 方法（额外保险）
        /// </summary>
        [HarmonyPatch(typeof(CharacterInputControl), "Update")]
        [HarmonyPrefix]
        public static bool CharacterInputControl_Update_Prefix()
        {
            return !_shouldBlockGameInput;
        }

        /// <summary>
        /// Patch InputManager.InputActived 属性（额外保险）
        /// </summary>
        [HarmonyPatch(typeof(InputManager), "InputActived", MethodType.Getter)]
        [HarmonyPostfix]
        public static void InputActived_Postfix(ref bool __result)
        {
            if (_shouldBlockGameInput)
            {
                __result = false;
            }
        }
    }
}

