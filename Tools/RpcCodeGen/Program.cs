using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using DuckyNet.Shared.RPC;

namespace RpcCodeGen
{
    class Program
    {
        static void Main(string[] args)
        {
            // 0. 清理旧的生成文件
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var solutionDir = FindSolutionDirectory(currentDir);
            CleanGeneratedFiles(solutionDir);
            
            // 1. 加载目标程序集
            var sharedDll = Path.Combine(solutionDir, "Shared", "bin", "Debug", "netstandard2.1", "DuckyNet.Shared.dll");
            if (!File.Exists(sharedDll))
            {
                Console.WriteLine($"未找到程序集: {sharedDll}");
                return;
            }
            var asm = Assembly.LoadFrom(sharedDll);

            // 2. 扫描带有 RpcServiceAttribute 的接口
            var rpcServiceAttr = asm.GetType("DuckyNet.Shared.RPC.RpcServiceAttribute");
            var interfaces = asm.GetTypes().Where(t => t.IsInterface && t.GetCustomAttributes(rpcServiceAttr, false).Any()).ToList();

            // 3. 生成客户端代理和服务端分发器
            var serverToClientAttr = asm.GetType("DuckyNet.Shared.RPC.ServerToClientAttribute");
            foreach (var iface in interfaces)
            {
                var attr = iface.GetCustomAttributes(rpcServiceAttr, false).FirstOrDefault();
                var serviceName = (string)rpcServiceAttr.GetProperty("ServiceName").GetValue(attr);
                GenerateClientProxy(iface, serviceName);
                GenerateServerDispatcher(iface, serviceName);
                
                // 检查是否有ServerToClient方法，生成广播代理
                var hasServerToClient = iface.GetMethods().Any(m => m.GetCustomAttributes(serverToClientAttr, false).Any());
                if (hasServerToClient)
                {
                    GenerateBroadcastProxy(iface, serviceName);
                    GenerateClientCallProxy(iface, serviceName);
                }
            }

            // 4. 收集所有参数/返回类型，生成类型注册代码
            GenerateTypeRegister(interfaces);
        }

        static void GenerateClientProxy(Type iface, string serviceName)
        {
            var sb = new StringBuilder();
            var ns = iface.Namespace + ".Generated";
            var className = iface.Name.TrimStart('I') + "ClientProxy";
            
            // 收集所有需要的命名空间
            var namespaces = CollectNamespaces(iface);
            
            sb.AppendLine($"using System;");
            sb.AppendLine($"using System.Linq;");
            sb.AppendLine($"using System.Threading.Tasks;");
            sb.AppendLine($"using DuckyNet.Shared.RPC;");
            foreach (var n in namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"using {n};");
            }
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine($"{{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// 客户端代理 - 用于调用服务器方法");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {className}");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        private readonly IClientContext _ctx;");
            sb.AppendLine($"        public {className}(IClientContext ctx) => _ctx = ctx;");
            sb.AppendLine();
            
            foreach (var m in iface.GetMethods())
            {
                var retType = SimplifyTypeName(m.ReturnType);
                var parameters = m.GetParameters();
                
                // 过滤掉 IClientContext 参数（客户端调用服务器时不需要传递）
                var clientParams = parameters.Where(p => p.ParameterType != typeof(IClientContext)).ToArray();
                var paramList = string.Join(", ", clientParams.Select(p => SimplifyTypeName(p.ParameterType) + " " + p.Name));
                var argNames = string.Join(", ", clientParams.Select(p => p.Name));
                
                if (m.ReturnType.FullName.StartsWith("System.Threading.Tasks.Task"))
                {
                    var genericArg = m.ReturnType.GenericTypeArguments.Length > 0 ? SimplifyTypeName(m.ReturnType.GenericTypeArguments[0]) : "object";
                    sb.AppendLine($"        public {retType} {m.Name}({paramList}) => _ctx.InvokeAsync<{iface.FullName}, {genericArg}>(\"{m.Name}\"{(argNames.Length > 0 ? ", " + argNames : "")});");
                }
                else
                {
                    sb.AppendLine($"        public {retType} {m.Name}({paramList}) => _ctx.Invoke<{iface.FullName}>(\"{m.Name}\"{(argNames.Length > 0 ? ", " + argNames : "")});");
                }
            }
            sb.AppendLine($"    }}");
            sb.AppendLine($"}}");
            
            var solutionDir = FindSolutionDirectory(AppDomain.CurrentDomain.BaseDirectory);
            var outputDir = Path.Combine(solutionDir, "Shared", "Generated");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"{className}.cs"), sb.ToString());
        }

        static void GenerateServerDispatcher(Type iface, string serviceName)
        {
            var sb = new StringBuilder();
            var ns = iface.Namespace + ".Generated";
            var className = iface.Name.TrimStart('I') + "ServerDispatcher";
            
            // 收集所有需要的命名空间
            var namespaces = CollectNamespaces(iface);
            
            sb.AppendLine($"using System;");
            sb.AppendLine($"using System.Threading.Tasks;");
            sb.AppendLine($"using DuckyNet.Shared.RPC;");
            foreach (var n in namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"using {n};");
            }
            sb.AppendLine($"namespace {ns}\n{{");
            sb.AppendLine($"    public class {className}\n    {{");
            sb.AppendLine($"        private readonly {iface.FullName} _impl;\n        public {className}({iface.FullName} impl) => _impl = impl;\n");
            sb.AppendLine($"        public object Dispatch(string method, object[] args, IClientContext ctx)\n        {{");
            sb.AppendLine($"            switch (method)\n            {{");
            foreach (var m in iface.GetMethods())
            {
                var parameters = m.GetParameters();
                var hasClientContext = parameters.Length > 0 && parameters[0].ParameterType == typeof(IClientContext);
                
                var argList = new List<string>();
                int argIndex = 0;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType == typeof(IClientContext))
                    {
                        argList.Add("ctx");
                    }
                    else
                    {
                        argList.Add($"({SimplifyTypeName(parameters[i].ParameterType)})args[{argIndex}]");
                        argIndex++;
                    }
                }
                
                // 处理 void 返回类型
                if (m.ReturnType == typeof(void))
                {
                    sb.AppendLine($"                case \"{m.Name}\": _impl.{m.Name}({string.Join(", ", argList)}); return null;");
                }
                else
                {
                    sb.AppendLine($"                case \"{m.Name}\": return _impl.{m.Name}({string.Join(", ", argList)});");
                }
            }
            sb.AppendLine($"                default: throw new Exception(\"Unknown method\");\n            }}\n        }}");
            sb.AppendLine("    }\n}");
            var solutionDir = FindSolutionDirectory(AppDomain.CurrentDomain.BaseDirectory);
            var outputDir = Path.Combine(solutionDir, "Shared", "Generated");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"{className}.cs"), sb.ToString());
        }

        static void GenerateBroadcastProxy(Type iface, string serviceName)
        {
            var sb = new StringBuilder();
            var ns = iface.Namespace + ".Generated";
            var className = iface.Name.TrimStart('I') + "BroadcastProxy";
            
            // 收集所有需要的命名空间
            var namespaces = CollectNamespaces(iface);
            
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            foreach (var n in namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"using {n};");
            }
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// 广播代理 - 用于向所有客户端发送消息");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {className} : {iface.FullName}");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly object _server;");
            sb.AppendLine($"        public {className}(object server) => _server = server;");
            sb.AppendLine();
            
            foreach (var m in iface.GetMethods())
            {
                var retType = SimplifyTypeName(m.ReturnType);
                var parameters = m.GetParameters();
                var paramList = string.Join(", ", parameters.Select(p => SimplifyTypeName(p.ParameterType) + " " + p.Name));
                var argNames = string.Join(", ", parameters.Select(p => p.Name));
                var argArray = parameters.Length > 0 ? $", {argNames}" : "";
                
                if (m.ReturnType == typeof(void))
                {
                    sb.AppendLine($"        public {retType} {m.Name}({paramList})");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            var method = _server.GetType().GetMethod(\"BroadcastToAll\").MakeGenericMethod(typeof({iface.FullName}));");
                    sb.AppendLine($"            method.Invoke(_server, new object[] {{ \"{m.Name}\", new object[] {{ {argNames} }} }});");
                    sb.AppendLine("        }");
                }
                else if (m.ReturnType.FullName.StartsWith("System.Threading.Tasks.Task"))
                {
                    sb.AppendLine($"        public {retType} {m.Name}({paramList})");
                    sb.AppendLine("        {");
                    sb.AppendLine("            // 注意: 广播方法不支持返回值");
                    sb.AppendLine($"            var method = _server.GetType().GetMethod(\"BroadcastToAll\").MakeGenericMethod(typeof({iface.FullName}));");
                    sb.AppendLine($"            method.Invoke(_server, new object[] {{ \"{m.Name}\", new object[] {{ {argNames} }} }});");
                    if (m.ReturnType.GenericTypeArguments.Length > 0)
                    {
                        sb.AppendLine($"            return Task.FromResult(default({SimplifyTypeName(m.ReturnType.GenericTypeArguments[0])}));");
                    }
                    else
                    {
                        sb.AppendLine("            return Task.CompletedTask;");
                    }
                    sb.AppendLine("        }");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            var solutionDir = FindSolutionDirectory(AppDomain.CurrentDomain.BaseDirectory);
            var outputDir = Path.Combine(solutionDir, "Shared", "Generated");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"{className}.cs"), sb.ToString());
        }

        static void GenerateClientCallProxy(Type iface, string serviceName)
        {
            var sb = new StringBuilder();
            var ns = iface.Namespace + ".Generated";
            var className = iface.Name.TrimStart('I') + "ClientCallProxy";
            
            // 收集所有需要的命名空间
            var namespaces = CollectNamespaces(iface);
            
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using DuckyNet.Shared.RPC;");
            foreach (var n in namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"using {n};");
            }
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// 单客户端调用代理 - 用于向特定客户端发送消息");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {className} : {iface.FullName}");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly IClientContext _client;");
            sb.AppendLine($"        public {className}(IClientContext client) => _client = client;");
            sb.AppendLine();
            
            foreach (var m in iface.GetMethods())
            {
                var retType = SimplifyTypeName(m.ReturnType);
                var parameters = m.GetParameters();
                var paramList = string.Join(", ", parameters.Select(p => SimplifyTypeName(p.ParameterType) + " " + p.Name));
                var argNames = string.Join(", ", parameters.Select(p => p.Name));
                
                if (m.ReturnType == typeof(void))
                {
                    sb.AppendLine($"        public {retType} {m.Name}({paramList}) => _client.Invoke<{iface.FullName}>(\"{m.Name}\"{(argNames.Length > 0 ? ", " + argNames : "")});");
                }
                else if (m.ReturnType.FullName.StartsWith("System.Threading.Tasks.Task"))
                {
                    var genericArg = m.ReturnType.GenericTypeArguments.Length > 0 ? SimplifyTypeName(m.ReturnType.GenericTypeArguments[0]) : "object";
                    sb.AppendLine($"        public {retType} {m.Name}({paramList}) => _client.InvokeAsync<{iface.FullName}, {genericArg}>(\"{m.Name}\"{(argNames.Length > 0 ? ", " + argNames : "")});");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            var solutionDir = FindSolutionDirectory(AppDomain.CurrentDomain.BaseDirectory);
            var outputDir = Path.Combine(solutionDir, "Shared", "Generated");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"{className}.cs"), sb.ToString());
        }

        static void GenerateTypeRegister(List<Type> interfaces)
        {
            // 收集所有需要序列化的类型
            var allTypes = new HashSet<Type>();
            
            foreach (var iface in interfaces)
            {
                foreach (var method in iface.GetMethods())
                {
                    // 添加参数类型
                    foreach (var param in method.GetParameters())
                    {
                        AddSerializableType(allTypes, param.ParameterType);
                    }
                    
                    // 添加返回值类型
                    AddSerializableType(allTypes, method.ReturnType);
                }
            }
            
            // 生成类型注册代码
            var sb = new StringBuilder();
            sb.AppendLine("// 自动生成的类型注册代码");
            sb.AppendLine("// 由 RpcCodeGen 工具自动生成，请勿手动修改");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace DuckyNet.Shared.RPC.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 自动生成的 RPC 序列化类型注册表");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class RpcTypeRegistry");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 获取所有需要序列化的类型");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static List<Type> GetSerializableTypes()");
            sb.AppendLine("        {");
            sb.AppendLine("            return new List<Type>");
            sb.AppendLine("            {");
            sb.AppendLine("                // 基础类型");
            sb.AppendLine("                typeof(string),");
            sb.AppendLine("                typeof(int),");
            sb.AppendLine("                typeof(long),");
            sb.AppendLine("                typeof(float),");
            sb.AppendLine("                typeof(double),");
            sb.AppendLine("                typeof(bool),");
            sb.AppendLine("                typeof(byte[]),");
            sb.AppendLine("                typeof(object[]),");
            sb.AppendLine("                typeof(DateTime),");
            sb.AppendLine();
            sb.AppendLine("                // RPC 消息类型");
            sb.AppendLine("                typeof(DuckyNet.Shared.RPC.RpcMessage),");
            sb.AppendLine("                typeof(DuckyNet.Shared.RPC.RpcResponse),");
            sb.AppendLine();
            sb.AppendLine("                // 应用数据类型 (自动发现)");
            
            foreach (var type in allTypes.OrderBy(t => t.FullName))
            {
                sb.AppendLine($"                typeof({GetTypeofString(type)}),");
            }
            
            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            var solutionDir = FindSolutionDirectory(AppDomain.CurrentDomain.BaseDirectory);
            var outputDir = Path.Combine(solutionDir, "Shared", "Generated");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "RpcTypeRegistry.cs"), sb.ToString());
            
            Console.WriteLine($"[CodeGen] Generated type registry with {allTypes.Count} types");
        }
        
        static void AddSerializableType(HashSet<Type> types, Type type)
        {
            // 跳过不需要序列化的类型
            if (type == null || 
                type == typeof(void) || 
                type == typeof(IClientContext) ||
                type.IsInterface ||
                type.IsPrimitive ||
                type == typeof(string) ||
                type == typeof(DateTime) ||
                type.IsGenericTypeDefinition)
            {
                return;
            }
            
            // 跳过所有 Task 类型（包括 Task 和 Task<T>）
            if (type == typeof(Task) || 
                (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)))
            {
                // Task 本身不能被序列化
                // 对于 Task<T>，只序列化其结果类型 T
                if (type.IsGenericType)
                {
                    var genericArg = type.GetGenericArguments()[0];
                    AddSerializableType(types, genericArg);
                }
                return;
            }
            
            // 处理数组类型
            if (type.IsArray)
            {
                types.Add(type);
                AddSerializableType(types, type.GetElementType());
                return;
            }
            
            // 添加自定义类型
            if (!types.Contains(type))
            {
                types.Add(type);
            }
        }

        /// <summary>
        /// 清理旧的生成文件
        /// </summary>
        static void CleanGeneratedFiles(string solutionDir)
        {
            var generatedDir = Path.Combine(solutionDir, "Shared", "Generated");
            if (!Directory.Exists(generatedDir))
            {
                Console.WriteLine("[CodeGen] Generated 目录不存在，将创建");
                Directory.CreateDirectory(generatedDir);
                return;
            }
            
            // 删除所有 .cs 文件（所有生成的文件都是 .cs 文件）
            var files = Directory.GetFiles(generatedDir, "*.cs");
            var deletedCount = 0;
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                    Console.WriteLine($"[CodeGen] 已删除旧文件: {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CodeGen] 删除文件失败 {Path.GetFileName(file)}: {ex.Message}");
                }
            }
            
            if (deletedCount > 0)
            {
                Console.WriteLine($"[CodeGen] 已清理 {deletedCount} 个旧生成文件");
            }
            else
            {
                Console.WriteLine("[CodeGen] 没有找到需要清理的旧文件");
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

        static string SimplifyTypeName(Type type)
        {
            if (type == typeof(void)) return "void";
            if (type == typeof(int)) return "int";
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(long)) return "long";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(DateTime)) return "DateTime";

            // 处理数组类型
            if (type.IsArray)
            {
                return SimplifyTypeName(type.GetElementType()) + "[]";
            }

            // 处理泛型类型
            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(Task<>))
                {
                    var argType = SimplifyTypeName(type.GenericTypeArguments[0]);
                    return $"Task<{argType}>";
                }
                // 可以扩展其他泛型类型
            }

            // 对于自定义类型，返回简化的名称（去掉命名空间前缀，如果在当前上下文中）
            return type.Name;
        }
        
        /// <summary>
        /// 生成用于 typeof() 的类型字符串
        /// </summary>
        static string GetTypeofString(Type type)
        {
            // 处理数组类型
            if (type.IsArray)
            {
                return GetTypeofString(type.GetElementType()) + "[]";
            }
            
            // 处理泛型类型
            if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();
                
                // 获取泛型类型的完整名称（不包含泛型参数）
                var genericTypeName = genericTypeDef.FullName;
                if (genericTypeName == null)
                {
                    return type.FullName;
                }
                
                // 移除泛型参数占位符 `1, `2 等
                var tickIndex = genericTypeName.IndexOf('`');
                if (tickIndex > 0)
                {
                    genericTypeName = genericTypeName.Substring(0, tickIndex);
                }
                
                // 构建泛型参数字符串
                var argStrings = genericArgs.Select(GetTypeofString);
                return $"{genericTypeName}<{string.Join(", ", argStrings)}>";
            }
            
            // 对于非泛型类型，直接返回完整名称
            return type.FullName ?? type.Name;
        }
        
        /// <summary>
        /// 收集接口中所有参数和返回值类型所需的命名空间
        /// </summary>
        static HashSet<string> CollectNamespaces(Type iface)
        {
            var namespaces = new HashSet<string>();
            
            foreach (var method in iface.GetMethods())
            {
                // 收集参数类型的命名空间
                foreach (var param in method.GetParameters())
                {
                    AddNamespace(namespaces, param.ParameterType);
                }
                
                // 收集返回值类型的命名空间
                AddNamespace(namespaces, method.ReturnType);
            }
            
            // 移除System命名空间（已经默认包含）和当前命名空间
            namespaces.Remove("System");
            namespaces.Remove("System.Threading.Tasks");
            namespaces.Remove("DuckyNet.Shared.RPC");
            namespaces.Remove(iface.Namespace);
            namespaces.Remove(iface.Namespace + ".Generated");
            
            return namespaces;
        }
        
        /// <summary>
        /// 添加类型所属的命名空间
        /// </summary>
        static void AddNamespace(HashSet<string> namespaces, Type type)
        {
            if (type == null || type == typeof(void) || type.IsPrimitive || type == typeof(string))
            {
                return;
            }
            
            // 处理数组类型
            if (type.IsArray)
            {
                AddNamespace(namespaces, type.GetElementType());
                return;
            }
            
            // 处理泛型类型（如 Task<T>）
            if (type.IsGenericType)
            {
                // 添加泛型定义的命名空间
                var genericTypeDef = type.GetGenericTypeDefinition();
                if (!string.IsNullOrEmpty(genericTypeDef.Namespace))
                {
                    namespaces.Add(genericTypeDef.Namespace);
                }
                
                // 递归处理泛型参数
                foreach (var arg in type.GetGenericArguments())
                {
                    AddNamespace(namespaces, arg);
                }
                return;
            }
            
            // 添加类型的命名空间
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                namespaces.Add(type.Namespace);
            }
        }
    }
}
