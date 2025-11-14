using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// RPC 代码生成器 - 自动生成客户端代理、服务端分发器等代码
    /// </summary>
    public static class RpcCodeGenerator
    {
        /// <summary>
        /// 生成所有 RPC 相关代码
        /// </summary>
        /// <param name="solutionDir">解决方案根目录</param>
        /// <param name="sharedAssemblyPath">Shared 程序集路径</param>
        public static void GenerateAll(string solutionDir, string? sharedAssemblyPath = null)
        {
            // 默认路径
            if (string.IsNullOrEmpty(sharedAssemblyPath))
            {
                sharedAssemblyPath = Path.Combine(solutionDir, "Shared", "bin", "Debug", "netstandard2.1", "DuckyNet.Shared.dll");
            }

            if (!File.Exists(sharedAssemblyPath))
            {
                throw new FileNotFoundException($"未找到程序集: {sharedAssemblyPath}");
            }

            // 加载 Shared 程序集（包含服务接口定义）
            var sharedAsm = Assembly.LoadFrom(sharedAssemblyPath);
            
            // 从当前程序集获取 RPC 属性类型
            var rpcAsm = typeof(RpcCodeGenerator).Assembly;
            var rpcServiceAttr = typeof(Messages.RpcServiceAttribute);
            var serverToClientAttr = typeof(Messages.ServerToClientAttribute);

            // 从 Shared 程序集中扫描带有 RpcServiceAttribute 的接口
            var interfaces = sharedAsm.GetTypes()
                .Where(t => t.IsInterface && t.GetCustomAttributes(rpcServiceAttr, false).Any())
                .ToList();

            // 清理旧的生成文件
            CleanGeneratedFiles(solutionDir);

            // 生成代码
            foreach (var iface in interfaces)
            {
                var attr = iface.GetCustomAttributes(rpcServiceAttr, false).FirstOrDefault();
                var serviceName = attr != null 
                    ? ((Messages.RpcServiceAttribute)attr).ServiceName 
                    : iface.Name;

                GenerateClientProxy(iface, serviceName, solutionDir);
                GenerateServerDispatcher(iface, serviceName, solutionDir);

                // 检查是否有ServerToClient方法，生成发送代理和处理函数扩展
                var hasServerToClient = iface.GetMethods().Any(m => m.GetCustomAttributes(serverToClientAttr, false).Any());
                if (hasServerToClient)
                {
                    GenerateSendProxy(iface, serviceName, solutionDir);
                    GenerateClientCallProxy(iface, serviceName, solutionDir);
                    GenerateMethodHandlerExtensions(iface, serviceName, serverToClientAttr, solutionDir);
                }
            }

            // 收集所有参数/返回类型，生成类型注册代码
            GenerateTypeRegister(interfaces, solutionDir);
        }

        static void GenerateClientProxy(Type iface, string serviceName, string solutionDir)
        {
            var sb = new StringBuilder();
            var ns = iface.Namespace + ".Generated";
            var className = (iface.Name.StartsWith("I") && iface.Name.Length > 1 && char.IsUpper(iface.Name[1])) 
                ? iface.Name.Substring(1) + "ClientProxy"
                : iface.Name + "ClientProxy";
            
            var namespaces = CollectNamespaces(iface);
            
            sb.AppendLine($"using System;");
            sb.AppendLine($"using System.Linq;");
            sb.AppendLine($"using System.Threading.Tasks;");
            sb.AppendLine($"using DuckyNet.RPC;");
            sb.AppendLine($"using DuckyNet.RPC.Context;");
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
                var clientParams = parameters.Where(p => p.ParameterType != typeof(Context.IClientContext)).ToArray();
                var paramList = string.Join(", ", clientParams.Select(p => SimplifyTypeName(p.ParameterType) + " " + p.Name));
                var argNames = string.Join(", ", clientParams.Select(p => p.Name));
                
                if (m.ReturnType.FullName?.StartsWith("System.Threading.Tasks.Task") == true)
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
            
            var outputDir = Path.Combine(solutionDir, "Shared", "Generated");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"{className}.cs"), sb.ToString());
        }

        static void GenerateServerDispatcher(Type iface, string serviceName, string solutionDir)
        {
            var sb = new StringBuilder();
            var ns = iface.Namespace + ".Generated";
            var className = (iface.Name.StartsWith("I") && iface.Name.Length > 1 && char.IsUpper(iface.Name[1])) 
                ? iface.Name.Substring(1) + "ServerDispatcher"
                : iface.Name + "ServerDispatcher";
            
            var namespaces = CollectNamespaces(iface);
            
            sb.AppendLine($"using System;");
            sb.AppendLine($"using System.Threading.Tasks;");
            sb.AppendLine($"using DuckyNet.RPC;");
            sb.AppendLine($"using DuckyNet.RPC.Context;");
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
                var argList = new List<string>();
                int argIndex = 0;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType == typeof(Context.IClientContext))
                    {
                        argList.Add("ctx");
                    }
                    else
                    {
                        argList.Add($"({SimplifyTypeName(parameters[i].ParameterType)})args[{argIndex}]");
                        argIndex++;
                    }
                }
                
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
            var outputDir = Path.Combine(solutionDir, "Shared", "Generated");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"{className}.cs"), sb.ToString());
        }

        static void GenerateSendProxy(Type iface, string serviceName, string solutionDir)
        {
            var sb = new StringBuilder();
            var ns = iface.Namespace + ".Generated";
            var className = (iface.Name.StartsWith("I") && iface.Name.Length > 1 && char.IsUpper(iface.Name[1])) 
                ? iface.Name.Substring(1) + "SendProxy"
                : iface.Name + "SendProxy";
            
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
            sb.AppendLine($"    /// 发送代理 - 用于向满足条件的客户端发送消息（使用过滤器）");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {className} : {iface.FullName}");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly object _server;");
            sb.AppendLine("        private readonly Func<string, bool> _predicate;");
            sb.AppendLine($"        public {className}(object server, Func<string, bool> predicate)");
            sb.AppendLine("        {");
            sb.AppendLine("            _server = server;");
            sb.AppendLine("            _predicate = predicate;");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            foreach (var m in iface.GetMethods())
            {
                var retType = SimplifyTypeName(m.ReturnType);
                var parameters = m.GetParameters();
                var paramList = string.Join(", ", parameters.Select(p => SimplifyTypeName(p.ParameterType) + " " + p.Name));
                var argNames = string.Join(", ", parameters.Select(p => p.Name));
                
                if (m.ReturnType == typeof(void))
                {
                    sb.AppendLine($"        public {retType} {m.Name}({paramList})");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            var method = _server.GetType().GetMethod(\"SendTo\").MakeGenericMethod(typeof({iface.FullName}));");
                    sb.AppendLine($"            method.Invoke(_server, new object[] {{ _predicate, \"{m.Name}\", new object[] {{ {argNames} }} }});");
                    sb.AppendLine("        }");
                }
                else if (m.ReturnType.FullName?.StartsWith("System.Threading.Tasks.Task") == true)
                {
                    sb.AppendLine($"        public {retType} {m.Name}({paramList})");
                    sb.AppendLine("        {");
                    sb.AppendLine("            // 注意: 发送方法不支持返回值");
                    sb.AppendLine($"            var method = _server.GetType().GetMethod(\"SendTo\").MakeGenericMethod(typeof({iface.FullName}));");
                    sb.AppendLine($"            method.Invoke(_server, new object[] {{ _predicate, \"{m.Name}\", new object[] {{ {argNames} }} }});");
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
            
            var outputDir = Path.Combine(solutionDir, "Shared", "Generated");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"{className}.cs"), sb.ToString());
        }

        static void GenerateClientCallProxy(Type iface, string serviceName, string solutionDir)
        {
            var sb = new StringBuilder();
            var ns = iface.Namespace + ".Generated";
            var className = (iface.Name.StartsWith("I") && iface.Name.Length > 1 && char.IsUpper(iface.Name[1])) 
                ? iface.Name.Substring(1) + "ClientCallProxy"
                : iface.Name + "ClientCallProxy";
            
            var namespaces = CollectNamespaces(iface);
            
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using DuckyNet.RPC;");
            sb.AppendLine("using DuckyNet.RPC.Context;");
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
                else if (m.ReturnType.FullName?.StartsWith("System.Threading.Tasks.Task") == true)
                {
                    var genericArg = m.ReturnType.GenericTypeArguments.Length > 0 ? SimplifyTypeName(m.ReturnType.GenericTypeArguments[0]) : "object";
                    sb.AppendLine($"        public {retType} {m.Name}({paramList}) => _client.InvokeAsync<{iface.FullName}, {genericArg}>(\"{m.Name}\"{(argNames.Length > 0 ? ", " + argNames : "")});");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            var outputDir = Path.Combine(solutionDir, "Shared", "Generated");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"{className}.cs"), sb.ToString());
        }

        static void GenerateMethodHandlerExtensions(Type iface, string serviceName, Type serverToClientAttr, string solutionDir)
        {
            var sb = new StringBuilder();
            var ns = iface.Namespace + ".Generated";
            var regClassName = (iface.Name.StartsWith("I") && iface.Name.Length > 1 && char.IsUpper(iface.Name[1])) 
                ? iface.Name.Substring(1) + "Reg"
                : iface.Name + "Reg";
            
            var namespaces = CollectNamespaces(iface);
            
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using DuckyNet.RPC.Core;");
            sb.AppendLine("using DuckyNet.RPC.Context;");
            foreach (var n in namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"using {n};");
            }
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            
            // 生成注册器类
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {iface.Name} 方法注册器 - 极简流畅API");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {regClassName}");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly RpcClient _client;");
            sb.AppendLine($"        internal {regClassName}(RpcClient client) => _client = client;");
            sb.AppendLine();
            
            foreach (var m in iface.GetMethods())
            {
                if (!m.GetCustomAttributes(serverToClientAttr, false).Any())
                    continue;
                
                var parameters = m.GetParameters();
                var returnType = m.ReturnType;
                GenerateRegMethod(sb, iface, m, parameters, returnType, regClassName);
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
            
            // 生成扩展方法
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {iface.Name} 注册扩展");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static class {iface.Name}RegExtensions");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static {regClassName} Reg<{iface.Name}>(this RpcClient client)");
            sb.AppendLine($"            => new {regClassName}(client);");
            sb.AppendLine("    }");
            sb.AppendLine();
            
            // 服务端版本
            var serverRegClassName = regClassName.Replace("Reg", "ServerReg");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {iface.Name} 服务端方法注册器 - 极简流畅API");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {serverRegClassName}");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly RpcServer _server;");
            sb.AppendLine($"        internal {serverRegClassName}(RpcServer server) => _server = server;");
            sb.AppendLine();
            
            foreach (var m in iface.GetMethods())
            {
                if (!m.GetCustomAttributes(serverToClientAttr, false).Any())
                    continue;
                
                var parameters = m.GetParameters();
                var returnType = m.ReturnType;
                GenerateRegMethodForServer(sb, iface, m, parameters, returnType, serverRegClassName);
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
            
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {iface.Name} 服务端注册扩展");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static class {iface.Name}ServerRegExtensions");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static {serverRegClassName} Reg<{iface.Name}>(this RpcServer server)");
            sb.AppendLine($"            => new {serverRegClassName}(server);");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            var outputDir = Path.Combine(solutionDir, "Shared", "Generated");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"{regClassName}.cs"), sb.ToString());
        }

        static void GenerateRegMethod(StringBuilder sb, Type iface, MethodInfo method, ParameterInfo[] parameters, Type returnType, string regClassName)
        {
            var methodName = method.Name;
            var userParams = parameters.Where(p => p.ParameterType != typeof(Context.IClientContext)).ToArray();
            var paramNames = string.Join(", ", userParams.Select(p => p.Name));
            
            string delegateType;
            if (returnType == typeof(void))
            {
                if (userParams.Length == 0)
                    delegateType = "Action";
                else if (userParams.Length == 1)
                    delegateType = $"Action<{SimplifyTypeName(userParams[0].ParameterType)}>";
                else if (userParams.Length == 2)
                    delegateType = $"Action<{SimplifyTypeName(userParams[0].ParameterType)}, {SimplifyTypeName(userParams[1].ParameterType)}>";
                else
                    delegateType = $"Action<{string.Join(", ", userParams.Select(p => SimplifyTypeName(p.ParameterType)))}>";
            }
            else
            {
                var returnTypeName = SimplifyTypeName(returnType);
                if (userParams.Length == 0)
                    delegateType = $"Func<{returnTypeName}>";
                else if (userParams.Length == 1)
                    delegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, {returnTypeName}>";
                else if (userParams.Length == 2)
                    delegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, {SimplifyTypeName(userParams[1].ParameterType)}, {returnTypeName}>";
                else
                    delegateType = $"Func<{string.Join(", ", userParams.Select(p => SimplifyTypeName(p.ParameterType)))}, {returnTypeName}>";
            }
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 注册 {methodName} 处理函数（自动调用 next）");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public {regClassName} {methodName}({delegateType} handler)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _client.RegisterMethodHandler<{iface.FullName}>(\"{methodName}\", ");
            sb.AppendLine($"                RpcMethodHandlerWrapper.Wrap(handler));");
            sb.AppendLine("            return this;");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            string nextDelegateType;
            if (returnType == typeof(void))
            {
                if (userParams.Length == 0)
                    nextDelegateType = "Func<RpcMethodHandler?, Task>";
                else if (userParams.Length == 1)
                    nextDelegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, RpcMethodHandler?, Task>";
                else if (userParams.Length == 2)
                    nextDelegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, {SimplifyTypeName(userParams[1].ParameterType)}, RpcMethodHandler?, Task>";
                else
                    nextDelegateType = $"Func<{string.Join(", ", userParams.Select(p => SimplifyTypeName(p.ParameterType)))}, RpcMethodHandler?, Task>";
            }
            else
            {
                var returnTypeName = SimplifyTypeName(returnType);
                if (userParams.Length == 0)
                    nextDelegateType = $"Func<RpcMethodHandler?, Task<{returnTypeName}>>";
                else if (userParams.Length == 1)
                    nextDelegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, RpcMethodHandler?, Task<{returnTypeName}>>";
                else if (userParams.Length == 2)
                    nextDelegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, {SimplifyTypeName(userParams[1].ParameterType)}, RpcMethodHandler?, Task<{returnTypeName}>>";
                else
                    nextDelegateType = $"Func<{string.Join(", ", userParams.Select(p => SimplifyTypeName(p.ParameterType)))}, RpcMethodHandler?, Task<{returnTypeName}>>";
            }
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 注册 {methodName} 处理函数（支持 next）");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public {regClassName} {methodName}({nextDelegateType} handler)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _client.RegisterMethodHandler<{iface.FullName}>(\"{methodName}\", ");
            sb.AppendLine($"                async (parameters, ctx, next) =>");
            sb.AppendLine("                {");
            
            int paramIndex = 0;
            foreach (var param in userParams)
            {
                sb.AppendLine($"                    var {param.Name} = ({SimplifyTypeName(param.ParameterType)})parameters![{paramIndex}]!;");
                paramIndex++;
            }
            
            if (returnType == typeof(void))
            {
                sb.AppendLine($"                    await handler({paramNames}{(paramNames.Length > 0 ? ", " : "")}next);");
                sb.AppendLine("                    return null;");
            }
            else
            {
                sb.AppendLine($"                    return await handler({paramNames}{(paramNames.Length > 0 ? ", " : "")}next);");
            }
            
            sb.AppendLine("                });");
            sb.AppendLine("            return this;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        static void GenerateRegMethodForServer(StringBuilder sb, Type iface, MethodInfo method, ParameterInfo[] parameters, Type returnType, string serverRegClassName)
        {
            var methodName = method.Name;
            var userParams = parameters.Where(p => p.ParameterType != typeof(Context.IClientContext)).ToArray();
            var paramNames = string.Join(", ", userParams.Select(p => p.Name));
            
            string delegateType;
            if (returnType == typeof(void))
            {
                if (userParams.Length == 0)
                    delegateType = "Action";
                else if (userParams.Length == 1)
                    delegateType = $"Action<{SimplifyTypeName(userParams[0].ParameterType)}>";
                else if (userParams.Length == 2)
                    delegateType = $"Action<{SimplifyTypeName(userParams[0].ParameterType)}, {SimplifyTypeName(userParams[1].ParameterType)}>";
                else
                    delegateType = $"Action<{string.Join(", ", userParams.Select(p => SimplifyTypeName(p.ParameterType)))}>";
            }
            else
            {
                var returnTypeName = SimplifyTypeName(returnType);
                if (userParams.Length == 0)
                    delegateType = $"Func<{returnTypeName}>";
                else if (userParams.Length == 1)
                    delegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, {returnTypeName}>";
                else if (userParams.Length == 2)
                    delegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, {SimplifyTypeName(userParams[1].ParameterType)}, {returnTypeName}>";
                else
                    delegateType = $"Func<{string.Join(", ", userParams.Select(p => SimplifyTypeName(p.ParameterType)))}, {returnTypeName}>";
            }
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 注册 {methodName} 处理函数（自动调用 next）");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public {serverRegClassName} {methodName}({delegateType} handler)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _server.RegisterMethodHandler<{iface.FullName}>(\"{methodName}\", ");
            sb.AppendLine($"                RpcMethodHandlerWrapper.Wrap(handler));");
            sb.AppendLine("            return this;");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            string nextDelegateType;
            if (returnType == typeof(void))
            {
                if (userParams.Length == 0)
                    nextDelegateType = "Func<RpcMethodHandler?, Task>";
                else if (userParams.Length == 1)
                    nextDelegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, RpcMethodHandler?, Task>";
                else if (userParams.Length == 2)
                    nextDelegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, {SimplifyTypeName(userParams[1].ParameterType)}, RpcMethodHandler?, Task>";
                else
                    nextDelegateType = $"Func<{string.Join(", ", userParams.Select(p => SimplifyTypeName(p.ParameterType)))}, RpcMethodHandler?, Task>";
            }
            else
            {
                var returnTypeName = SimplifyTypeName(returnType);
                if (userParams.Length == 0)
                    nextDelegateType = $"Func<RpcMethodHandler?, Task<{returnTypeName}>>";
                else if (userParams.Length == 1)
                    nextDelegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, RpcMethodHandler?, Task<{returnTypeName}>>";
                else if (userParams.Length == 2)
                    nextDelegateType = $"Func<{SimplifyTypeName(userParams[0].ParameterType)}, {SimplifyTypeName(userParams[1].ParameterType)}, RpcMethodHandler?, Task<{returnTypeName}>>";
                else
                    nextDelegateType = $"Func<{string.Join(", ", userParams.Select(p => SimplifyTypeName(p.ParameterType)))}, RpcMethodHandler?, Task<{returnTypeName}>>";
            }
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 注册 {methodName} 处理函数（支持 next）");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public {serverRegClassName} {methodName}({nextDelegateType} handler)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _server.RegisterMethodHandler<{iface.FullName}>(\"{methodName}\", ");
            sb.AppendLine($"                async (parameters, ctx, next) =>");
            sb.AppendLine("                {");
            
            int paramIndex = 0;
            foreach (var param in userParams)
            {
                sb.AppendLine($"                    var {param.Name} = ({SimplifyTypeName(param.ParameterType)})parameters![{paramIndex}]!;");
                paramIndex++;
            }
            
            if (returnType == typeof(void))
            {
                sb.AppendLine($"                    await handler({paramNames}{(paramNames.Length > 0 ? ", " : "")}next);");
                sb.AppendLine("                    return null;");
            }
            else
            {
                sb.AppendLine($"                    return await handler({paramNames}{(paramNames.Length > 0 ? ", " : "")}next);");
            }
            
            sb.AppendLine("                });");
            sb.AppendLine("            return this;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        static void GenerateTypeRegister(List<Type> interfaces, string solutionDir)
        {
            var allTypes = new HashSet<Type>();
            
            foreach (var iface in interfaces)
            {
                foreach (var method in iface.GetMethods())
                {
                    foreach (var param in method.GetParameters())
                    {
                        AddSerializableType(allTypes, param.ParameterType);
                    }
                    AddSerializableType(allTypes, method.ReturnType);
                }
            }
            
            var sb = new StringBuilder();
            sb.AppendLine("// 自动生成的类型注册代码");
            sb.AppendLine("// 由 DuckyNet.RPC.Core.RpcCodeGenerator 自动生成，请勿手动修改");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace DuckyNet.RPC.Generated");
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
            sb.AppendLine("                typeof(DuckyNet.RPC.Messages.RpcMessage),");
            sb.AppendLine("                typeof(DuckyNet.RPC.Messages.RpcResponse),");
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
            
            var outputDir = Path.Combine(solutionDir, "Shared", "Generated");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "RpcTypeRegistry.cs"), sb.ToString());
        }

        static void AddSerializableType(HashSet<Type> types, Type type)
        {
            if (type == null || 
                type == typeof(void) || 
                type == typeof(Context.IClientContext) ||
                type.IsInterface ||
                type.IsPrimitive ||
                type == typeof(string) ||
                type == typeof(DateTime) ||
                type.IsGenericTypeDefinition)
            {
                return;
            }
            
            if (type == typeof(Task) || 
                (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)))
            {
                if (type.IsGenericType)
                {
                    var genericArg = type.GetGenericArguments()[0];
                    AddSerializableType(types, genericArg);
                }
                return;
            }
            
            if (type.IsArray)
            {
                types.Add(type);
                AddSerializableType(types, type.GetElementType()!);
                return;
            }
            
            if (!types.Contains(type))
            {
                types.Add(type);
            }
        }

        static void CleanGeneratedFiles(string solutionDir)
        {
            var generatedDir = Path.Combine(solutionDir, "Shared", "Generated");
            if (!Directory.Exists(generatedDir))
            {
                Directory.CreateDirectory(generatedDir);
                return;
            }
            
            var files = Directory.GetFiles(generatedDir, "*.cs");
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RpcCodeGenerator] 删除文件失败 {Path.GetFileName(file)}: {ex.Message}");
                }
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

            if (type.IsArray)
            {
                return SimplifyTypeName(type.GetElementType()!) + "[]";
            }

            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(Task<>))
                {
                    var argType = SimplifyTypeName(type.GenericTypeArguments[0]);
                    return $"Task<{argType}>";
                }
            }

            return type.Name;
        }

        static string GetTypeofString(Type type)
        {
            if (type.IsArray)
            {
                return GetTypeofString(type.GetElementType()!) + "[]";
            }
            
            if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();
                var genericTypeName = genericTypeDef.FullName;
                if (genericTypeName == null)
                {
                    return type.FullName ?? type.Name;
                }
                
                var tickIndex = genericTypeName.IndexOf('`');
                if (tickIndex > 0)
                {
                    genericTypeName = genericTypeName.Substring(0, tickIndex);
                }
                
                var argStrings = genericArgs.Select(GetTypeofString);
                return $"{genericTypeName}<{string.Join(", ", argStrings)}>";
            }
            
            return type.FullName ?? type.Name;
        }

        static HashSet<string> CollectNamespaces(Type iface)
        {
            var namespaces = new HashSet<string>();
            
            foreach (var method in iface.GetMethods())
            {
                foreach (var param in method.GetParameters())
                {
                    AddNamespace(namespaces, param.ParameterType);
                }
                AddNamespace(namespaces, method.ReturnType);
            }
            
            namespaces.Remove("System");
            namespaces.Remove("System.Threading.Tasks");
            namespaces.Remove("DuckyNet.RPC");
            namespaces.Remove(iface.Namespace);
            namespaces.Remove(iface.Namespace + ".Generated");
            
            return namespaces;
        }

        static void AddNamespace(HashSet<string> namespaces, Type type)
        {
            if (type == null || type == typeof(void) || type.IsPrimitive || type == typeof(string))
            {
                return;
            }
            
            if (type.IsArray)
            {
                AddNamespace(namespaces, type.GetElementType()!);
                return;
            }
            
            if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();
                if (!string.IsNullOrEmpty(genericTypeDef.Namespace))
                {
                    namespaces.Add(genericTypeDef.Namespace);
                }
                
                foreach (var arg in type.GetGenericArguments())
                {
                    AddNamespace(namespaces, arg);
                }
                return;
            }
            
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                namespaces.Add(type.Namespace);
            }
        }
    }
}

