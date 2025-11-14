using Microsoft.Extensions.DependencyInjection;
using DuckyNet.RPC;
using DuckyNet.Server.Events;
using DuckyNet.Server.Managers;

namespace DuckyNet.Server.Core
{
    /// <summary>
    /// 服务器初始化器
    /// [REFACTOR] 阶段1：过渡方案 - 初始化静态依赖
    /// TODO(REFACTOR): 阶段2会移除所有静态依赖，使用完全的依赖注入
    /// </summary>
    public static class ServerInitializer
    {
        /// <summary>
        /// 初始化静态依赖（兼容性过渡）
        /// 当前一些辅助类（ServerEventPublisher）仍然使用静态字段，需要手动初始化
        /// </summary>
        public static void InitializeStaticDependencies(IServiceProvider serviceProvider)
        {
            // 初始化事件发布器（用于服务层便捷发布事件）
            var eventBus = serviceProvider.GetRequiredService<EventBus>();
            ServerEventPublisher.Initialize(eventBus);

            // 注意：RoomBroadcastHelper 已被 BroadcastManager 替代，通过依赖注入使用
        }
    }
}

