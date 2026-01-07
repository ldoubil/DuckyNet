using System;
using DuckyNet.RPC.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DuckyNet.Server.Plugin
{
    /// <summary>
    /// 插件接口
    /// 所有插件必须实现此接口
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// 插件名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 插件版本
        /// </summary>
        string Version { get; }

        /// <summary>
        /// 插件作者
        /// </summary>
        string Author { get; }

        /// <summary>
        /// 插件描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 插件加载时调用
        /// </summary>
        /// <param name="context">插件上下文，提供对服务器资源的访问</param>
        void OnLoad(IPluginContext context);

        /// <summary>
        /// 注册插件依赖注入服务
        /// </summary>
        /// <param name="services">服务集合</param>
        void ConfigureServices(IServiceCollection services);

        /// <summary>
        /// 注册插件 RPC 服务
        /// </summary>
        /// <param name="server">RPC 服务器</param>
        void ConfigureRpc(RpcServer server);

        /// <summary>
        /// 注册 Web 端点（可选）
        /// </summary>
        /// <param name="endpoints">端点路由构建器</param>
        void ConfigureWeb(IEndpointRouteBuilder endpoints);

        /// <summary>
        /// 插件卸载时调用
        /// </summary>
        void OnUnload();

        /// <summary>
        /// 每帧更新时调用（约 60 FPS）
        /// </summary>
        void OnUpdate();
    }
}
