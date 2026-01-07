using System;

namespace DuckyNet.Shared.Events
{
    /// <summary>
    /// 领域事件标记接口
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// 事件名称（默认使用类型名）
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 事件发生时间（UTC）
        /// </summary>
        DateTimeOffset OccurredAt { get; }
    }

    /// <summary>
    /// 统一事件基类
    /// </summary>
    public abstract class EventBase : IEvent
    {
        protected EventBase()
        {
            Name = GetType().Name;
            OccurredAt = DateTimeOffset.UtcNow;
        }

        public string Name { get; }

        public DateTimeOffset OccurredAt { get; }
    }
}
