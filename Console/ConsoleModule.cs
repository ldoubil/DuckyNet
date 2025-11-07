using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DuckyNet.Console
{
    /// <summary>
    /// 通用控制台模块，负责创建控制台窗口、Unity 日志重定向和彩色输出
    /// </summary>
    public static class ConsoleModule
    {
        private static IntPtr _consoleWindow = IntPtr.Zero;
        private static StreamWriter? _consoleWriter;
        private static bool _isInitialized = false;

        #region Windows API 声明
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCP(uint wCodePageID);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll")]
        private static extern uint GetConsoleCP();

        [DllImport("kernel32.dll")]
        private static extern uint GetConsoleOutputCP();

        private const int SW_SHOW = 5;
        private const uint CP_UTF8 = 65001; // UTF-8 代码页
        #endregion

        /// <summary>
        /// 初始化控制台模块
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                UnityEngine.Debug.LogWarning("[DuckyNet.Console] ConsoleModule 已经初始化，跳过");
                return;
            }

            try
            {
                CreateConsoleWindow();
                SetupUnityLogRedirection();
                _isInitialized = true;
                
                UnityEngine.Debug.Log("[DuckyNet.Console] ConsoleModule 初始化成功");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckyNet.Console] ConsoleModule 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理控制台和日志重定向
        /// </summary>
        public static void Cleanup()
        {
            if (!_isInitialized) return;

            try
            {
                // 移除 Unity 日志监听
                Application.logMessageReceived -= OnUnityLogReceived;

                // 关闭控制台写入流
                if (_consoleWriter != null)
                {
                    _consoleWriter.Close();
                    _consoleWriter = null;
                }

                // 释放控制台窗口
                if (_consoleWindow != IntPtr.Zero)
                {
                    FreeConsole();
                    _consoleWindow = IntPtr.Zero;
                }

                _isInitialized = false;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckyNet.Console] ConsoleModule 清理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 向控制台写入消息（支持指定颜色）
        /// </summary>
        private static void Write(string message, ConsoleColor color)
        {
            if (!_isInitialized || _consoleWriter == null) return;

            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string formattedMessage = $"[{timestamp}] {message}";

                System.Console.ForegroundColor = color;
                _consoleWriter.WriteLine(formattedMessage);
                _consoleWriter.Flush();
                System.Console.ResetColor();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckyNet.Console] 控制台写入失败: {ex.Message}");
            }
        }

        #region 私有方法

        /// <summary>
        /// 创建控制台窗口
        /// </summary>
        private static void CreateConsoleWindow()
        {
            try
            {
                // 分配控制台
                if (!AllocConsole())
                {
                    throw new InvalidOperationException("无法分配控制台窗口");
                }

                // 获取控制台窗口句柄
                _consoleWindow = GetConsoleWindow();
                if (_consoleWindow == IntPtr.Zero)
                {
                    throw new InvalidOperationException("无法获取控制台窗口句柄");
                }

                // 显示控制台窗口
                ShowWindow(_consoleWindow, SW_SHOW);

                // 设置 UTF-8 编码（支持中文）
                SetConsoleCP(CP_UTF8);
                SetConsoleOutputCP(CP_UTF8);
                System.Console.OutputEncoding = System.Text.Encoding.UTF8;
                System.Console.InputEncoding = System.Text.Encoding.UTF8;

                // 设置控制台标题
                System.Console.Title = "DuckyNet Console - Unity Log Viewer";

                // 创建输出流
                _consoleWriter = new StreamWriter(System.Console.OpenStandardOutput(), System.Text.Encoding.UTF8)
                {
                    AutoFlush = true
                };
                System.Console.SetOut(_consoleWriter);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckyNet.Console] 创建控制台窗口失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 设置 Unity 日志重定向
        /// </summary>
        private static void SetupUnityLogRedirection()
        {
            try
            {
                Application.logMessageReceived += OnUnityLogReceived;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckyNet.Console] 设置 Unity 日志重定向失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Unity 日志回调 - 解析并渲染带颜色的日志
        /// </summary>
        private static void OnUnityLogReceived(string logString, string stackTrace, LogType type)
        {
            try
            {
                // 根据 Unity 日志类型选择颜色
                ConsoleColor color = type switch
                {
                    LogType.Error => ConsoleColor.Red,
                    LogType.Warning => ConsoleColor.Yellow,
                    LogType.Log => ConsoleColor.White,
                    LogType.Exception => ConsoleColor.DarkRed,
                    LogType.Assert => ConsoleColor.Magenta,
                    _ => ConsoleColor.Gray
                };

                // 输出日志消息
                Write(logString, color);

                // 如果是错误或异常，输出堆栈跟踪
                if ((type == LogType.Error || type == LogType.Exception) && !string.IsNullOrEmpty(stackTrace))
                {
                    Write(stackTrace, ConsoleColor.DarkRed);
                }
            }
            catch
            {
                // 避免日志回调中的异常导致无限循环
            }
        }

        #endregion
    }
}

