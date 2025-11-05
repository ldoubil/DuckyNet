using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DuckyNet.Client.Core.Utils
{
    /// <summary>
    /// 独立的控制台模块，负责创建调试控制台窗口、日志重定向和彩色输出
    /// 
    /// 🔥 条件编译说明：
    /// - DEBUG 模式：创建控制台窗口，输出所有日志
    /// - RELEASE 模式：不创建控制台窗口，所有控制台方法为空操作
    /// 
    /// 编译配置：
    /// - Debug 编译：包含所有控制台功能
    /// - Release 编译：移除所有控制台代码，减少性能开销
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
        /// 🔥 只在 DEBUG 模式下创建控制台窗口
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                UnityEngine.Debug.LogWarning("[DuckyNet] ConsoleModule 已经初始化，跳过");
                return;
            }

#if DEBUG
            try
            {
                CreateConsoleWindow();
                SetupUnityLogRedirection();
                _isInitialized = true;
                
                Write("[DuckyNet] ConsoleModule 初始化成功 ✓ (DEBUG 模式)");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckyNet] ConsoleModule 初始化失败: {ex.Message}");
            }
#else
            UnityEngine.Debug.Log("[DuckyNet] ConsoleModule 跳过初始化 (Release 模式)");
            _isInitialized = false;
#endif
        }

        /// <summary>
        /// 清理控制台和日志重定向
        /// </summary>
        public static void Cleanup()
        {
#if DEBUG
            if (!_isInitialized) return;

            try
            {
                // 移除 Unity 日志监听
                Application.logMessageReceived -= OnUnityLogReceived;
                Write("[DuckyNet] Unity 日志重定向已清理");

                // 关闭控制台写入流
                if (_consoleWriter != null)
                {
                    WriteSeparator("控制台即将关闭");
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
                UnityEngine.Debug.LogError($"[DuckyNet] ConsoleModule 清理失败: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// 向控制台写入消息（支持自动颜色）
        /// 🔥 只在 DEBUG 模式下输出到控制台
        /// </summary>
        public static void Write(string message, ConsoleColor? color = null)
        {
#if DEBUG
            if (!_isInitialized || _consoleWriter == null) return;

            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string formattedMessage = $"[{timestamp}] {message}";

                // 自动选择颜色（如果未指定）
                ConsoleColor selectedColor = color ?? GetColorForMessage(message);

                Console.ForegroundColor = selectedColor;
                _consoleWriter.WriteLine(formattedMessage);
                _consoleWriter.Flush();
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckyNet] 控制台写入失败: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// 输出分隔线
        /// 🔥 只在 DEBUG 模式下输出
        /// </summary>
        public static void WriteSeparator(string? title = null)
        {
#if DEBUG
            if (!_isInitialized || _consoleWriter == null) return;

            try
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                
                if (string.IsNullOrEmpty(title))
                {
                    _consoleWriter.WriteLine("════════════════════════════════════════════════════════════════");
                }
                else
                {
                    int totalLength = 64;
                    int titleLength = title.Length + 2; // 加上两边的空格
                    int sideLength = (totalLength - titleLength) / 2;
                    
                    string leftSide = new string('═', sideLength);
                    string rightSide = new string('═', totalLength - sideLength - titleLength);
                    
                    _consoleWriter.WriteLine($"{leftSide} {title} {rightSide}");
                }
                
                _consoleWriter.Flush();
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckyNet] 控制台分隔线输出失败: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// 输出欢迎信息
        /// 🔥 只在 DEBUG 模式下输出
        /// </summary>
        public static void WriteWelcome()
        {
#if DEBUG
            WriteSeparator("DuckyNet 调试控制台");
            Write($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", ConsoleColor.Cyan);
            Write($"版本: v2.2", ConsoleColor.Cyan);
            Write($"中文测试: 你好世界！🦆", ConsoleColor.Green);
            WriteSeparator();
#endif
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
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.InputEncoding = System.Text.Encoding.UTF8;

                // 设置控制台标题
                Console.Title = "DuckyNet Mod - Debug Console";

                // 创建输出流
                _consoleWriter = new StreamWriter(Console.OpenStandardOutput(), System.Text.Encoding.UTF8)
                {
                    AutoFlush = true
                };
                Console.SetOut(_consoleWriter);

                // 输出欢迎信息
                WriteWelcome();

                // 验证代码页
                uint inputCP = GetConsoleCP();
                uint outputCP = GetConsoleOutputCP();
                Write($"控制台代码页: 输入={inputCP}, 输出={outputCP}", ConsoleColor.DarkGray);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckyNet] 创建控制台窗口失败: {ex.Message}");
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
                Write("[DuckyNet] Unity 日志重定向已启用", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckyNet] 设置 Unity 日志重定向失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Unity 日志回调
        /// </summary>
        private static void OnUnityLogReceived(string logString, string stackTrace, LogType type)
        {
            try
            {
                // 获取日志类型前缀和颜色
                (string prefix, ConsoleColor color) = type switch
                {
                    LogType.Error => ("[ERROR]", ConsoleColor.Red),
                    LogType.Warning => ("[WARNING]", ConsoleColor.Yellow),
                    LogType.Log => ("[INFO]", ConsoleColor.White),
                    LogType.Exception => ("[EXCEPTION]", ConsoleColor.DarkRed),
                    LogType.Assert => ("[ASSERT]", ConsoleColor.Magenta),
                    _ => ("[LOG]", ConsoleColor.Gray)
                };

                // 输出日志消息
                Write($"{prefix} {logString}", color);

                // 如果是错误或异常，输出堆栈跟踪
                if ((type == LogType.Error || type == LogType.Exception) && !string.IsNullOrEmpty(stackTrace))
                {
                    Write($"Stack Trace:\n{stackTrace}", ConsoleColor.DarkRed);
                }
            }
            catch
            {
                // 避免日志回调中的异常导致无限循环
            }
        }

        /// <summary>
        /// 根据消息内容自动选择颜色
        /// </summary>
        private static ConsoleColor GetColorForMessage(string message)
        {
            // 错误相关
            if (message.Contains("[ERROR]") || message.Contains("错误") || 
                message.Contains("失败") || message.Contains("Exception"))
                return ConsoleColor.Red;

            // 警告相关
            if (message.Contains("[WARNING]") || message.Contains("警告") || 
                message.Contains("Warning"))
                return ConsoleColor.Yellow;

            // 成功相关
            if (message.Contains("成功") || message.Contains("完成") || 
                message.Contains("✓") || message.Contains("已连接"))
                return ConsoleColor.Green;

            // RPC 相关
            if (message.Contains("RPC") || message.Contains("调用") || 
                message.Contains("Invoke"))
                return ConsoleColor.Cyan;

            // UI 相关
            if (message.Contains("UI") || message.Contains("窗口") || 
                message.Contains("Window"))
                return ConsoleColor.Blue;

            // DuckyNet 模组标签
            if (message.Contains("[DuckyNet]"))
                return ConsoleColor.Magenta;

            // 初始化相关
            if (message.Contains("初始化") || message.Contains("加载") || 
                message.Contains("Initialize"))
                return ConsoleColor.Cyan;

            // 聊天相关
            if (message.Contains("[Chat]") || message.Contains("聊天"))
                return ConsoleColor.DarkCyan;

            // 房间相关
            if (message.Contains("Room") || message.Contains("房间"))
                return ConsoleColor.DarkYellow;

            // 玩家相关
            if (message.Contains("Player") || message.Contains("玩家"))
                return ConsoleColor.DarkGreen;

            // 默认白色
            return ConsoleColor.White;
        }

        #endregion
    }
}

