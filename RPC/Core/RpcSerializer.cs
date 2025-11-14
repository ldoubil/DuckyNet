using System;
using System.Collections.Generic;
using System.IO;
using NetSerializer;
using DuckyNet.RPC.Messages;
using RpcMessage = DuckyNet.RPC.Messages.RpcMessage;
using RpcResponse = DuckyNet.RPC.Messages.RpcResponse;
using RpcMessageType = DuckyNet.RPC.Messages.RpcMessageType;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// RPC 序列化工具
    /// 封装 NetSerializer 用于参数和返回值的序列化
    /// </summary>
    public class RpcSerializer
    {
        private readonly Serializer _serializer;
        private static readonly Lazy<RpcSerializer> _instance = new Lazy<RpcSerializer>(() => new RpcSerializer());

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static RpcSerializer Instance => _instance.Value;

        private RpcSerializer()
        {
            // 使用自动生成的类型注册表
            // 由 DuckyNet.RPC.Core.RpcCodeGenerator 自动生成，无需手动维护
            List<Type> types;
            
            try
            {
                // 尝试使用生成的类型注册表
                var registryType = Type.GetType("DuckyNet.RPC.Generated.RpcTypeRegistry, DuckyNet.Shared");
                if (registryType != null)
                {
                    var method = registryType.GetMethod("GetSerializableTypes");
                    types = (List<Type>)method!.Invoke(null, null)!;
                    Console.WriteLine($"[RpcSerializer] Loaded {types.Count} types from auto-generated registry");
                }
                else
                {
                    // 如果代码还没生成，使用后备类型列表
                    Console.WriteLine("[RpcSerializer] Auto-generated type registry not found, using fallback types");
                    types = GetFallbackTypes();
                }
            }
            catch (Exception ex)
            {
                // 出错时使用后备列表
                Console.WriteLine($"[RpcSerializer] Error loading type registry: {ex.Message}, using fallback");
                types = GetFallbackTypes();
            }

            _serializer = new Serializer(types);
        }
        
        /// <summary>
        /// 后备类型列表（用于代码生成前或出错时）
        /// </summary>
        private static List<Type> GetFallbackTypes()
        {
            return new List<Type>
            {
                typeof(RpcMessage),
                typeof(RpcResponse),
                typeof(string),
                typeof(int),
                typeof(long),
                typeof(float),
                typeof(double),
                typeof(bool),
                typeof(byte[]),
                typeof(object[]),
                typeof(DateTime)
                // 应用类型通过反射动态加载，无需在此硬编码
            };
        }

        /// <summary>
        /// 序列化对象（带类型标记）
        /// </summary>
        public byte[] Serialize(object obj)
        {
            using (var ms = new MemoryStream())
            {
                // 确定消息类型
                RpcMessageType messageType;
                if (obj is RpcMessage)
                {
                    messageType = RpcMessageType.Request;
                }
                else if (obj is RpcResponse)
                {
                    messageType = RpcMessageType.Response;
                }
                else
                {
                    // 不是RPC消息类型，直接序列化（不添加标记）
                    _serializer.Serialize(ms, obj);
                    return ms.ToArray();
                }

                // 写入类型标记
                ms.WriteByte((byte)messageType);

                // 序列化实际对象
                _serializer.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 序列化对象（不带类型标记，用于内部使用）
        /// </summary>
        private byte[] SerializeRaw(object obj)
        {
            using (var ms = new MemoryStream())
            {
                _serializer.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 反序列化对象（支持类型标记）
        /// </summary>
        public T Deserialize<T>(byte[] data)
        {
            try
            {
                using (var ms = new MemoryStream(data))
                {
                    // 检查是否是带标记的RPC消息
                    if (typeof(T) == typeof(RpcMessage) || typeof(T) == typeof(RpcResponse))
                    {
                        // 读取类型标记（需要至少1字节）
                        if (ms.Length < 1)
                        {
                            throw new InvalidOperationException(
                                $"Data too short to contain message type marker. Data length: {data?.Length ?? 0} bytes");
                        }

                        var typeByte = (byte)ms.ReadByte();
                        
                        // 验证是否是有效的消息类型标记
                        if (!Enum.IsDefined(typeof(RpcMessageType), typeByte))
                        {
                            throw new InvalidOperationException(
                                $"Invalid message type marker: 0x{typeByte:X2}. Expected Request (0x01) or Response (0x02)");
                        }

                        var messageType = (RpcMessageType)typeByte;

                        // 验证类型匹配
                        if (messageType == RpcMessageType.Request && typeof(T) != typeof(RpcMessage))
                        {
                            throw new InvalidCastException(
                                $"Message type mismatch: data is Request (RpcMessage), but expecting {typeof(T).FullName}");
                        }
                        if (messageType == RpcMessageType.Response && typeof(T) != typeof(RpcResponse))
                        {
                            throw new InvalidCastException(
                                $"Message type mismatch: data is Response (RpcResponse), but expecting {typeof(T).FullName}");
                        }
                    }

                    var obj = _serializer.Deserialize(ms);
                    if (obj == null)
                    {
                        throw new InvalidOperationException($"Deserialized object is null, expected type: {typeof(T).FullName}");
                    }
                    
                    // 如果类型完全匹配，直接转换
                    if (obj is T directMatch)
                    {
                        return directMatch;
                    }
                    
                    // 尝试强制转换
                    try
                    {
                        return (T)obj;
                    }
                    catch (InvalidCastException ex)
                    {
                        throw new InvalidCastException(
                            $"Cannot cast deserialized object from {obj.GetType().FullName} to {typeof(T).FullName}. " +
                            $"Object type assembly: {obj.GetType().Assembly.FullName}, " +
                            $"Target type assembly: {typeof(T).Assembly.FullName}. " +
                            $"Original error: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is InvalidCastException ice && (ice.Message.Contains("Cannot cast") || ice.Message.Contains("Message type mismatch")))
                {
                    // 已经是我们改进的错误信息，直接抛出
                    throw;
                }
                
                // 包装其他异常以提供更多上下文
                throw new InvalidOperationException(
                    $"Failed to deserialize to {typeof(T).FullName}. Data length: {data?.Length ?? 0} bytes. " +
                    $"Error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检测消息类型（不进行完整反序列化）
        /// </summary>
        public RpcMessageType? DetectMessageType(byte[] data)
        {
            if (data == null || data.Length < 1)
                return null;

            try
            {
                using (var ms = new MemoryStream(data))
                {
                    if (ms.Length < 1)
                        return null;
                        
                    var typeByte = (byte)ms.ReadByte();
                    if (Enum.IsDefined(typeof(RpcMessageType), typeByte))
                    {
                        return (RpcMessageType)typeByte;
                    }
                }
            }
            catch
            {
                // 忽略错误，返回 null 表示无法检测消息类型
            }

            return null;
        }

        /// <summary>
        /// 序列化参数数组（参数不需要类型标记）
        /// </summary>
        public byte[]? SerializeParameters(object?[]? parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return null;

            return SerializeRaw(parameters);
        }

        /// <summary>
        /// 反序列化参数数组
        /// </summary>
        public object?[]? DeserializeParameters(byte[]? data)
        {
            if (data == null || data.Length == 0)
                return null;

            return Deserialize<object[]>(data);
        }

    }
}

