using System;
using System.Collections.Generic;
using System.Linq;
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
                        // 转换参数类型以匹配方法签名
                        for (int i = 0; i < Math.Min(args.Length, parameters.Length - 1); i++)
                        {
                            var paramIndex = i + 1; // +1 因为第一个参数是 clientContext
                            var paramType = parameters[paramIndex].ParameterType;
                            invokeParams[paramIndex] = ConvertParameter(args[i], paramType);
                        }
                    }
                }
                else
                {
                    invokeParams = new object?[parameters.Length];
                    if (args != null)
                    {
                        // 转换参数类型以匹配方法签名
                        for (int i = 0; i < Math.Min(args.Length, parameters.Length); i++)
                        {
                            var paramType = parameters[i].ParameterType;
                            invokeParams[i] = ConvertParameter(args[i], paramType);
                        }
                    }
                }

                return method.Invoke(instance, invokeParams);
            };
        }

        /// <summary>
        /// 转换参数类型
        /// </summary>
        private object? ConvertParameter(object? value, Type targetType)
        {
            if (value == null)
                return null;

            var sourceType = value.GetType();

            // 如果已经是正确类型，直接返回
            if (targetType.IsInstanceOfType(value))
                return value;

            // 对于数组类型，检查元素类型是否匹配
            if (targetType.IsArray && sourceType.IsArray)
            {
                var targetElementType = targetType.GetElementType();
                var sourceElementType = sourceType.GetElementType();
                
                if (targetElementType != null)
                {
                    // 情况1: 如果源数组元素类型就是目标元素类型，直接返回
                    if (sourceElementType != null && 
                        (targetElementType.IsAssignableFrom(sourceElementType) || 
                         targetElementType.FullName == sourceElementType.FullName))
                    {
                        return value; // 可以安全转换
                    }
                    
                    // 情况2: 如果源数组是 object[]（反序列化后的常见情况），需要手动转换
                    if (sourceType == typeof(object[]))
                    {
                        try
                        {
                            var sourceArray = (Array)value;
                            var targetArray = Array.CreateInstance(targetElementType, sourceArray.Length);
                            
                            for (int i = 0; i < sourceArray.Length; i++)
                            {
                                var element = sourceArray.GetValue(i);
                                if (element != null)
                                {
                                    // 如果元素已经是目标类型，直接使用；否则尝试转换
                                    if (targetElementType.IsInstanceOfType(element))
                                    {
                                        targetArray.SetValue(element, i);
                                    }
                                    else
                                    {
                                        // 尝试类型转换（递归调用，但避免数组类型的无限递归）
                                        // 注意：这里我们只转换单个元素，不会再次进入数组转换分支
                                        object? convertedElement = element;
                                        var elementType = element.GetType();
                                        
                                        // 如果是可以直接赋值的情况
                                        if (targetElementType.IsAssignableFrom(elementType))
                                        {
                                            convertedElement = element;
                                        }
                                        // 如果是完全限定名匹配（不同程序集中的相同类型）
                                        else if (targetElementType.FullName == elementType.FullName)
                                        {
                                            convertedElement = element;
                                        }
                                        // 尝试强制转换（用于包装类型等情况）
                                        else
                                        {
                                            try
                                            {
                                                convertedElement = System.Convert.ChangeType(element, targetElementType);
                                            }
                                            catch
                                            {
                                                // 转换失败，最后尝试直接赋值（可能会抛出异常）
                                                convertedElement = element;
                                            }
                                        }
                                        
                                        // 尝试设置值，如果类型不匹配会抛出异常
                                        try
                                        {
                                            targetArray.SetValue(convertedElement, i);
                                        }
                                        catch (Exception setEx)
                                        {
                                            throw new InvalidCastException(
                                                $"Cannot set element at index {i}. Source type: {element?.GetType().FullName ?? "null"}, " +
                                                $"Target type: {targetElementType.FullName}, Converted type: {convertedElement?.GetType().FullName ?? "null"}. " +
                                                $"Error: {setEx.Message}", setEx);
                                        }
                                    }
                                }
                                else
                                {
                                    targetArray.SetValue(null, i);
                                }
                            }
                            
                            return targetArray;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidCastException(
                                $"Failed to convert object[] to {targetType.FullName}. Error: {ex.Message}");
                        }
                    }
                }
            }

            // 尝试使用 Convert.ChangeType（主要用于值类型）
            if (targetType.IsValueType || targetType == typeof(string))
            {
                try
                {
                    return System.Convert.ChangeType(value, targetType);
                }
                catch
                {
                    // 转换失败，继续尝试其他方法
                }
            }

            // 对于引用类型，检查是否可以直接赋值（继承关系）
            if (!targetType.IsValueType && value != null)
            {
                // 检查是否是继承关系
                if (targetType.IsAssignableFrom(sourceType))
                {
                    return value;
                }

                // 检查是否是接口实现
                if (targetType.IsInterface)
                {
                    var interfaces = sourceType.GetInterfaces();
                    if (interfaces.Any(i => i == targetType || i.IsAssignableFrom(targetType)))
                    {
                        return value;
                    }
                }

                // 检查完全限定名是否匹配（可能是不同程序集中的相同类型）
                if (targetType.FullName == sourceType.FullName)
                {
                    return value;
                }

                // 最后尝试直接返回并捕获异常
                try
                {
                    return value;
                }
                catch (Exception ex)
                {
                    throw new InvalidCastException($"Cannot convert {sourceType.FullName} ({sourceType.Assembly.FullName}) to {targetType.FullName} ({targetType.Assembly.FullName}). Error: {ex.Message}");
                }
            }

            throw new InvalidCastException($"Cannot convert {sourceType.FullName ?? "null"} to {targetType.FullName}");
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
                    var genericArgs = taskType.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        var resultProperty = taskType.GetProperty("Result");
                        if (resultProperty != null)
                        {
                            var taskResult = resultProperty.GetValue(task);
                            
                            // 检查是否是 VoidTaskResult（Task 的内部类型）
                            var resultType = taskResult?.GetType();
                            if (resultType != null && resultType.Name == "VoidTaskResult")
                            {
                                // VoidTaskResult 不应该被序列化，返回 null
                                return null;
                            }
                            
                            RpcLog.Info($"[RpcInvoker] Extracted result from Task<T>: {taskResult?.GetType().Name ?? "null"}");
                            return taskResult;
                        }
                    }
                }
                
                // Task (非泛型) 或 void 方法
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
