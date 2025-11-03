using DuckyNet.Server.Managers;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Events;

namespace DuckyNet.Server.Plugin
{
    /// <summary>
    /// 插件上下文接口
    /// 提供插件访问服务器资源的能力
    /// </summary>
    public interface IPluginContext
    {
        /// <summary>
        /// 玩家管理器
        /// </summary>
        PlayerManager PlayerManager { get; }

        /// <summary>
        /// 房间管理器
        /// </summary>
        RoomManager RoomManager { get; }

        /// <summary>
        /// RPC 服务器
        /// </summary>
        RpcServer RpcServer { get; }

        /// <summary>
        /// 事件总线
        /// </summary>
        IEventBus EventBus { get; }

        /// <summary>
        /// 日志记录器
        /// </summary>
        IPluginLogger Logger { get; }
    }
}

