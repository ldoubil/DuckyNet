using System;
using System.Collections.Generic;
using System.IO;
using NetSerializer;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.RPC;

namespace DuckyNet.Shared.RPC
{
    /// <summary>
    /// RPC 序列化工具
    /// 封装 NetSerializer 用于参数和返回值的序列化
    /// </summary>
    public class RpcSerializer
    {
        private readonly Serializer _serializer;
        private static RpcSerializer? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static RpcSerializer Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new RpcSerializer();
                        }
                    }
                }
                return _instance;
            }
        }

        private RpcSerializer()
        {
            // 使用自动生成的类型注册表
            // 由 RpcCodeGen 工具自动生成，无需手动维护
            List<Type> types;
            
            try
            {
                // 尝试使用生成的类型注册表
                var registryType = Type.GetType("DuckyNet.Shared.RPC.Generated.RpcTypeRegistry, DuckyNet.Shared");
                if (registryType != null)
                {
                    var method = registryType.GetMethod("GetSerializableTypes");
                    types = (List<Type>)method!.Invoke(null, null)!;
                    RpcLog.Info($"[RpcSerializer] Loaded {types.Count} types from auto-generated registry");
                }
                else
                {
                    // 如果代码还没生成，使用后备类型列表
                    RpcLog.Warning("[RpcSerializer] Auto-generated type registry not found, using fallback types");
                    types = GetFallbackTypes();
                }
            }
            catch (Exception ex)
            {
                // 出错时使用后备列表
                RpcLog.Error($"[RpcSerializer] Error loading type registry: {ex.Message}, using fallback");
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
                typeof(DateTime),
                // 应用类型（需要手动添加，建议运行代码生成器）
                typeof(PlayerInfo),
                typeof(LoginResult),
                typeof(PlayerStatus),
                typeof(MessageType),
                typeof(PlayerInfo[])
            };
        }

        /// <summary>
        /// 序列化对象
        /// </summary>
        public byte[] Serialize(object obj)
        {
            using (var ms = new MemoryStream())
            {
                _serializer.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 反序列化对象
        /// </summary>
        public T Deserialize<T>(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                return (T)_serializer.Deserialize(ms);
            }
        }

        /// <summary>
        /// 序列化参数数组
        /// </summary>
        public byte[]? SerializeParameters(object?[]? parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return null;

            return Serialize(parameters);
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
