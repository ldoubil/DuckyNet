using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace DuckyNet.Shared.RPC
{
    /// <summary>
    /// RPC 方法调用器
    /// 负责通过强类型委托调用注册的服务方法
    /// </summary>
    public class RpcInvoker
    {
        private readonly Dictionary<string, ServiceInfo> _services = new Dictionary<string, ServiceInfo>();

        // 定义统一的委托类型
        private delegate object? RpcMethodDelegate(object instance, object?[]? parameters, IClientContext? clientContext);

        private class ServiceInfo
        {
            public object Instance { get; set; } = null!;
            public Dictionary<string, RpcMethodDelegate> Methods { get; set; } = new Dictionary<string, RpcMethodDelegate>();
        }

        /// <summary>
        /// 注册服务
        /// </summary>
        public void RegisterService<TService>(object serviceInstance) where TService : class
        {
            var serviceType = typeof(TService);
            var serviceName = GetServiceName(serviceType);

            var serviceInfo = new ServiceInfo
            {
                Instance = serviceInstance
            };

            // 查找所有标记了 RpcMethod 的方法（包括派生特性）
            var methods = serviceType.GetMethods();
            foreach (var method in methods)
            {
                // inherit: true 确保能找到 ClientToServerAttribute 和 ServerToClientAttribute
                var rpcAttr = method.GetCustomAttribute<RpcMethodAttribute>(inherit: true);
                if (rpcAttr != null)
                {
                    var methodName = string.IsNullOrEmpty(rpcAttr.MethodName) ? method.Name : rpcAttr.MethodName;
                    serviceInfo.Methods[methodName] = CreateDelegate(method);
                }
            }

            _services[serviceName] = serviceInfo;
        }

        /// <summary>
        /// 创建强类型委托
        /// </summary>
        private RpcMethodDelegate CreateDelegate(MethodInfo method)
        {
            var parameters = method.GetParameters();
            bool hasClientContext = parameters.Length > 0 && parameters[0].ParameterType == typeof(IClientContext);

            return (instance, args, clientContext) =>
            {
                object?[] invokeParams;

                if (hasClientContext)
                {
                    invokeParams = new object?[parameters.Length];
                    invokeParams[0] = clientContext;
                    if (args != null)
                    {
                        Array.Copy(args, 0, invokeParams, 1, Math.Min(args.Length, parameters.Length - 1));
                    }
                }
                else
                {
                    invokeParams = args ?? Array.Empty<object?>();
                }

                return method.Invoke(instance, invokeParams);
            };
        }

        /// <summary>
        /// 调用服务方法
        /// </summary>
        public object? Invoke(string serviceName, string methodName, object?[]? parameters, IClientContext? clientContext = null)
        {
            if (!_services.TryGetValue(serviceName, out var serviceInfo))
            {
                throw new Exception($"Service '{serviceName}' not found");
            }

            if (!serviceInfo.Methods.TryGetValue(methodName, out var methodDelegate))
            {
                throw new Exception($"Method '{methodName}' not found in service '{serviceName}'");
            }

            try
            {
                return methodDelegate(serviceInfo.Instance, parameters, clientContext);
            }
            catch (TargetInvocationException ex)
            {
                // 重新抛出内部异常，保留原始堆栈跟踪
                throw ex.InnerException ?? ex;
            }
        }

        /// <summary>
        /// 异步调用服务方法
        /// </summary>
        public async Task<object?> InvokeAsync(string serviceName, string methodName, object?[]? parameters, IClientContext? clientContext = null)
        {
            var result = Invoke(serviceName, methodName, parameters, clientContext);
            
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                
                // 如果是 Task<T>，提取结果
                var taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    var resultProperty = taskType.GetProperty("Result");
                    if (resultProperty != null)
                    {
                        var taskResult = resultProperty.GetValue(task);
                        RpcLog.Info($"[RpcInvoker] Extracted result from Task<T>: {taskResult?.GetType().Name ?? "null"}");
                        return taskResult;
                    }
                }
                
                // Task (非泛型)
                return null;
            }
            
            // 非异步方法，直接返回结果
            return result;
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
    }
}
