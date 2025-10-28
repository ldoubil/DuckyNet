using System;

namespace DuckyNet.Shared.RPC
{
    /// <summary>
    /// 标记RPC服务接口
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class RpcServiceAttribute : Attribute
    {
        public string ServiceName { get; }

        public RpcServiceAttribute(string serviceName)
        {
            ServiceName = serviceName;
        }
    }

    /// <summary>
    /// 标记RPC方法
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class RpcMethodAttribute : Attribute
    {
        public string? MethodName { get; set; }
    }

    /// <summary>
    /// 标记客户端到服务器的RPC方法
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientToServerAttribute : RpcMethodAttribute
    {
    }

    /// <summary>
    /// 标记服务器到客户端的RPC方法
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerToClientAttribute : RpcMethodAttribute
    {
    }
}
