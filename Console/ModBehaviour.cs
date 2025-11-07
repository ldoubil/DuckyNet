using System;
using UnityEngine;
using HarmonyLib;

namespace DuckyNet.Console
{
    /// <summary>
    /// DuckyNet 控制台模组主行为类
    /// 提供增强的调试控制台功能
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        /// <summary>
        /// 全局实例
        /// </summary>
        public static ModBehaviour? Instance { get; private set; }

        /// <summary>
        /// Harmony 实例
        /// </summary>
        private static Harmony? _harmony;

        void Awake()
        {
            try
            {
                // 设置全局实例
                Instance = this;

                // 初始化控制台模块（Unity 日志重定向）
                ConsoleModule.Initialize();

                // 应用 Harmony 补丁
                ApplyHarmonyPatches();

                Debug.Log("[DuckyNet.Console] 控制台模组初始化完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckyNet.Console] 初始化失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        void Start()
        {
            try
            {
                // 输出 ASCII 艺术字
                Debug.Log("______            _          _   _      _   ");
                Debug.Log("|  _  \\          | |        | \\ | |    | |  ");
                Debug.Log("| | | |_   _  ___| | ___   _|  \\| | ___| |_ ");
                Debug.Log("| | | | | | |/ __| |/ / | | | . ` |/ _ \\ __|");
                Debug.Log("| |/ /| |_| | (__|   <| |_| | |\\  |  __/ |_ ");
                Debug.Log("|___/  \\__,_|\\___|_|\\_\\\\__, \\_| \\_/\\___|\\__|");
                Debug.Log("                        __/ |               ");
                Debug.Log("                       |___/                ");
                Debug.Log(" _____                       _      ");
                Debug.Log("/  __ \\                     | |     ");
                Debug.Log("| /  \\/ ___  _ __  ___  ___ | | ___ ");
                Debug.Log("| |    / _ \\| '_ \\/ __|/ _ \\| |/ _ \\");
                Debug.Log("| \\__/| (_) | | | \\__ | (_) | |  __/");
                Debug.Log(" \\____/\\___/|_| |_|___/\\___/|_|\\___|");
                Debug.Log("                                    ");
                Debug.Log("");
                Debug.Log("[DuckyNet.Console] 控制台模组启动完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckyNet.Console] 启动失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        void OnDestroy()
        {
            try
            {
                Debug.Log("[DuckyNet.Console] 正在卸载控制台模组...");
                
                // 移除 Harmony 补丁
                RemoveHarmonyPatches();

                // 清理实例
                Instance = null;

                Debug.Log("[DuckyNet.Console] 控制台模组已卸载");
                
                // 清理控制台模块
                ConsoleModule.Cleanup();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckyNet.Console] 卸载失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 应用 Harmony 补丁
        /// </summary>
        private void ApplyHarmonyPatches()
        {
            try
            {
                _harmony = new Harmony("com.duckynet.console");
                _harmony.PatchAll();
                Debug.Log("[DuckyNet.Console] Harmony 补丁已应用");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckyNet.Console] Harmony 补丁应用失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 移除 Harmony 补丁
        /// </summary>
        private void RemoveHarmonyPatches()
        {
            try
            {
                if (_harmony != null)
                {
                    _harmony.UnpatchAll(_harmony.Id);
                    _harmony = null;
                    Debug.Log("[DuckyNet.Console] Harmony 补丁已移除");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckyNet.Console] Harmony 补丁移除失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

