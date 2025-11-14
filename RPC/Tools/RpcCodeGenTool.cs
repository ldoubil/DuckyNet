using System;
using System.IO;
using DuckyNet.RPC.Core;

namespace DuckyNet.RPC.Tools
{
    /// <summary>
    /// RPC 代码生成工具入口点（仅在 Debug 模式下编译为可执行文件）
    /// </summary>
    public static class RpcCodeGenTool
    {
        /// <summary>
        /// 主入口点 - 用于命令行调用
        /// </summary>
        public static int Main(string[] args)
        {
            try
            {
                var currentDir = AppDomain.CurrentDomain.BaseDirectory;
                var solutionDir = FindSolutionDirectory(currentDir);
                
                var sharedDll = args.Length > 0 
                    ? args[0] 
                    : Path.Combine(solutionDir, "Shared", "bin", "Debug", "netstandard2.1", "DuckyNet.Shared.dll");
                
                Console.WriteLine("[RpcCodeGenerator] 开始生成代码...");
                Console.WriteLine($"[RpcCodeGenerator] 解决方案目录: {solutionDir}");
                Console.WriteLine($"[RpcCodeGenerator] Shared 程序集: {sharedDll}");
                
                RpcCodeGenerator.GenerateAll(solutionDir, sharedDll);
                
                Console.WriteLine("[RpcCodeGenerator] ✓ 代码生成完成");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RpcCodeGenerator] ✗ 错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static string FindSolutionDirectory(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "DuckyNet.sln")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not find solution directory containing DuckyNet.sln");
        }
    }
}

