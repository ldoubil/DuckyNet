using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DuckyNet.RPC.Messages;
using DuckyNet.RPC.Context;
using RpcMethodAttribute = DuckyNet.RPC.Messages.RpcMethodAttribute;
using RpcServiceAttribute = DuckyNet.RPC.Messages.RpcServiceAttribute;
using IClientContext = DuckyNet.RPC.Context.IClientContext;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// RPC 方法调用器
    /// 负责通过强类型委托调用注册的服务方法
    /// </summary>
    public class RpcInvoker
    {
        private readonly Dictionary<string, ServiceInfo> _services = new Dictionary<string, ServiceInfo>();
        private readonly RpcMiddlewarePipeline _middlewarePipeline = new RpcMiddlewarePipeline();

        // 定义统一的委托类型
        private delegate object? RpcMethodDelegate(object instance, object?[]? parameters, IClientContext? clientContext);

        private class ServiceInfo
        {
            public List<object> Instances { get; set; } = new List<object>();
            public Dictionary<string, List<RpcMethodDelegate>> Methods { get; set; } = new Dictionary<string, List<RpcMethodDelegate>>();
            public Dictionary<string, List<RpcMethodHandler>> MethodHandlers { get; set; } = new Dictionary<string, List<RpcMethodHandler>>();
        }

        /// <summary>
        /// 注册服务（支持多个处理器）
        /// </summary>
        public void RegisterService<TService>(object serviceInstance) where TService : class
        {
            var serviceType = typeof(TService);
            var serviceName = GetServiceName(serviceType);

            // 获取或创建服务信息
            if (!_services.TryGetValue(serviceName, out var serviceInfo))
            {
                serviceInfo = new ServiceInfo();
                _services[serviceName] = serviceInfo;
            }

            // 添加实例
            serviceInfo.Instances.Add(serviceInstance);

            // 查找所有标记了 RpcMethod 的方法（包括派生特性）
            var methods = serviceType.GetMethods();
            foreach (var method in methods)
            {
                // inherit: true 确保能找到 ClientToServerAttribute 和 ServerToClientAttribute
                var rpcAttr = method.GetCustomAttribute<RpcMethodAttribute>(inherit: true);
                if (rpcAttr != null)
                {
                    var methodName = string.IsNullOrEmpty(rpcAttr.MethodName) ? method.Name : rpcAttr.MethodName;
                    
                    // 获取或创建方法列表
                    if (!serviceInfo.Methods.TryGetValue(methodName, out var methodList))
                    {
                        methodList = new List<RpcMethodDelegate>();
                        serviceInfo.Methods[methodName] = methodList;
                    }

                    // 为这个实例创建委托并添加到列表
                    methodList.Add(CreateDelegate(serviceInstance, method));
                }
            }
        }

        /// <summary>
        /// 注册方法处理函数（支持 next() 调用，可以在多个地方注册）
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <param name="methodName">方法名称</param>
        /// <param name="handler">处理函数，可以调用 next() 继续执行下一个处理函数</param>
        public void RegisterMethodHandler<TService>(string methodName, RpcMethodHandler handler) where TService : class
        {
            var serviceType = typeof(TService);
            var serviceName = GetServiceName(serviceType);

            // 获取或创建服务信息
            if (!_services.TryGetValue(serviceName, out var serviceInfo))
            {
                serviceInfo = new ServiceInfo();
                _services[serviceName] = serviceInfo;
            }

            // 获取或创建处理函数列表
            if (!serviceInfo.MethodHandlers.TryGetValue(methodName, out var handlerList))
            {
                handlerList = new List<RpcMethodHandler>();
                serviceInfo.MethodHandlers[methodName] = handlerList;
            }

            handlerList.Add(handler);
        }

        /// <summary>
        /// 创建强类型委托
        /// </summary>
        private RpcMethodDelegate CreateDelegate(object instance, MethodInfo method)
        {
            var parameters = method.GetParameters();
            bool hasClientContext = parameters.Length > 0 && parameters[0].ParameterType == typeof(IClientContext);
            int paramOffset = hasClientContext ? 1 : 0;

            return (inst, args, clientContext) =>
            {
                var invokeParams = new object?[parameters.Length];
                
                // 设置 clientContext（如果有）
                if (hasClientContext)
                    invokeParams[0] = clientContext;

                // 转换参数类型以匹配方法签名
                if (args != null)
                {
                    for (int i = 0; i < Math.Min(args.Length, parameters.Length - paramOffset); i++)
                    {
                        var paramIndex = i + paramOffset;
                        invokeParams[paramIndex] = ConvertParameter(args[i], parameters[paramIndex].ParameterType);
                    }
                }

                return method.Invoke(instance, invokeParams);
            };
        }

        /// <summary>
        /// 转换参数类型（简化版 - NetSerializer 已处理大部分类型转换）
        /// </summary>
        private object? ConvertParameter(object? value, Type targetType)
        {
            if (value == null)
                return null;

            // 如果已经是正确类型，直接返回
            if (targetType.IsInstanceOfType(value))
                return value;

            // 对于数组类型，处理 object[] 到具体数组类型的转换
            if (targetType.IsArray && value is Array sourceArray)
            {
                var targetElementType = targetType.GetElementType();
                if (targetElementType == null)
                    throw new InvalidCastException($"Target array type {targetType.FullName} has no element type");

                var targetArray = Array.CreateInstance(targetElementType, sourceArray.Length);
                for (int i = 0; i < sourceArray.Length; i++)
                {
                    var element = sourceArray.GetValue(i);
                    targetArray.SetValue(targetElementType.IsInstanceOfType(element) ? element : null, i);
                }
                return targetArray;
            }

            // 尝试类型转换（主要用于值类型和字符串）
            if (targetType.IsValueType || targetType == typeof(string))
            {
                try
                {
                    return System.Convert.ChangeType(value, targetType);
                }
                catch
                {
                    // 转换失败，继续检查
                }
            }

            // 检查类型兼容性（继承关系、接口实现、完全限定名匹配）
            var sourceType = value.GetType();
            if (targetType.IsAssignableFrom(sourceType) || 
                targetType.FullName == sourceType.FullName)
            {
                return value;
            }

            // 无法转换，抛出异常
            throw new InvalidCastException(
                $"Cannot convert {sourceType.FullName} to {targetType.FullName}");
        }

        /// <summary>
        /// 调用服务方法（同步版本，执行所有处理器）
        /// </summary>
        public object? Invoke(string serviceName, string methodName, object?[]? parameters, IClientContext? clientContext = null)
        {
            if (!_services.TryGetValue(serviceName, out var serviceInfo))
            {
                throw new Exception($"Service '{serviceName}' not found");
            }

            // 优先使用处理函数链（支持 next）
            if (serviceInfo.MethodHandlers.TryGetValue(methodName, out var handlers) && handlers.Count > 0)
            {
                // 构建处理函数链并执行（异步转同步）
                var task = InvokeMethodHandlersAsync(handlers, parameters, clientContext);
                return task.GetAwaiter().GetResult();
            }

            // 回退到传统方法调用（执行所有注册的方法）
            if (!serviceInfo.Methods.TryGetValue(methodName, out var methodDelegates) || methodDelegates.Count == 0)
            {
                throw new Exception($"Method '{methodName}' not found in service '{serviceName}'");
            }

            // 执行所有方法（按注册顺序，所有方法都会执行）
            object? result = null;
            foreach (var methodDelegate in methodDelegates)
            {
                try
                {
                    result = methodDelegate(null!, parameters, clientContext);
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException ?? ex;
                }
            }

            return result;
        }

        /// <summary>
        /// 异步执行方法处理函数链（支持 next() 调用）
        /// </summary>
        private async Task<object?> InvokeMethodHandlersAsync(List<RpcMethodHandler> handlers, object?[]? parameters, IClientContext? clientContext)
        {
            if (handlers.Count == 0)
                return null;

            object? result = null;

            // 构建 next 委托链（从最后一个处理函数开始向前构建）
            RpcMethodHandler? next = null;
            
            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                var handler = handlers[i];
                var capturedNext = next; // 捕获当前的 next
                
                next = async (args, ctx, n) =>
                {
                    // 调用当前处理函数，传入下一个处理函数
                    var handlerResult = await handler(args, ctx, capturedNext);
                    result = handlerResult; // 保存结果
                    return handlerResult;
                };
            }

            // 执行第一个处理函数
            if (next != null)
            {
                result = await next(parameters, clientContext, null);
            }

            return result;
        }

        /// <summary>
        /// 添加中间件
        /// </summary>
        public RpcInvoker Use(IRpcMiddleware middleware)
        {
            _middlewarePipeline.Use(middleware);
            return this;
        }

        /// <summary>
        /// 异步调用服务方法（支持中间件）
        /// </summary>
        public async Task<object?> InvokeAsync(string serviceName, string methodName, object?[]? parameters, IClientContext? clientContext = null)
        {
            // 创建中间件上下文
            var context = new RpcMiddlewareContext
            {
                ServiceName = serviceName,
                MethodName = methodName,
                Parameters = parameters,
                ClientContext = clientContext
            };

            // 构建完整的中间件链：用户中间件 -> 默认执行中间件
            var fullPipeline = new RpcMiddlewarePipeline();
            foreach (var mw in _middlewarePipeline._middlewares)
            {
                fullPipeline.Use(mw);
            }
            fullPipeline.Use(new DefaultInvokeMiddleware(this));

            // 执行中间件管道
            await fullPipeline.ExecuteAsync(context);

            return context.Result;
        }

        /// <summary>
        /// 执行实际的方法调用（内部方法）
        /// </summary>
        private async Task<object?> ExecuteInvokeAsync(string serviceName, string methodName, object?[]? parameters, IClientContext? clientContext)
        {
            var result = Invoke(serviceName, methodName, parameters, clientContext);
            
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                
                // 如果是 Task<T>，提取结果
                if (task.GetType().IsGenericType)
                {
                    var taskResult = task.GetType().GetProperty("Result")?.GetValue(task);
                    // VoidTaskResult 不应该被序列化，返回 null
                    return taskResult?.GetType().Name == "VoidTaskResult" ? null : taskResult;
                }
                
                return null; // Task (非泛型) 或 void 方法
            }
            
            return result; // 非异步方法，直接返回结果
        }

        /// <summary>
        /// 获取服务名称
        /// </summary>
        private string GetServiceName(Type serviceType)
        {
            var attr = serviceType.GetCustomAttribute<RpcServiceAttribute>();
            return attr?.ServiceName ?? serviceType.Name;
        }

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        public bool IsServiceRegistered(string serviceName)
        {
            return _services.ContainsKey(serviceName);
        }

        /// <summary>
        /// 获取已注册的服务列表
        /// </summary>
        public IEnumerable<string> GetRegisteredServices()
        {
            return _services.Keys;
        }

        /// <summary>
        /// 默认执行中间件 - 实际调用服务方法或处理器链
        /// </summary>
        private class DefaultInvokeMiddleware : IRpcMiddleware
        {
            private readonly RpcInvoker _invoker;

            public DefaultInvokeMiddleware(RpcInvoker invoker)
            {
                _invoker = invoker;
            }

            public async Task InvokeAsync(RpcMiddlewareContext context, RpcMiddlewareDelegate next)
            {
                // 这是最后一个中间件，执行实际的方法调用
                if (!_invoker._services.TryGetValue(context.ServiceName, out var serviceInfo))
                {
                    throw new Exception($"Service '{context.ServiceName}' not found");
                }

                // 优先使用处理函数链（支持 next）
                if (serviceInfo.MethodHandlers.TryGetValue(context.MethodName, out var handlers) && handlers.Count > 0)
                {
                    context.Result = await _invoker.InvokeMethodHandlersAsync(handlers, context.Parameters, context.ClientContext);
                }
                else if (serviceInfo.Methods.TryGetValue(context.MethodName, out var methodDelegates) && methodDelegates.Count > 0)
                {
                    // 回退到传统方法调用
                    context.Result = await _invoker.ExecuteInvokeAsync(
                        context.ServiceName,
                        context.MethodName,
                        context.Parameters,
                        context.ClientContext);
                }
                else
                {
                    throw new Exception($"Method '{context.MethodName}' not found in service '{context.ServiceName}'");
                }

                context.IsHandled = true;
            }
        }
    }
}

