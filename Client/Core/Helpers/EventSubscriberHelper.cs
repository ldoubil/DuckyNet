using System;
using System.Collections.Generic;

namespace DuckyNet.Client.Core.Helpers
{
    /// <summary>
    /// EventBus 订阅辅助类 - 统一管理事件订阅，减少样板代码
    /// </summary>
    public class EventSubscriberHelper : IDisposable
    {
        private EventBus? _eventBus;
        private readonly List<IDisposableSubscription> _subscriptions = new List<IDisposableSubscription>();

        /// <summary>
        /// 获取 EventBus 实例（自动初始化）
        /// </summary>
        public EventBus EventBus
        {
            get
            {
                if (_eventBus == null && GameContext.IsInitialized)
                {
                    _eventBus = GameContext.Instance.EventBus;
                }
                return _eventBus ?? throw new InvalidOperationException("EventBus 未初始化，GameContext 可能尚未初始化");
            }
        }

        /// <summary>
        /// 订阅事件（自动管理，如果 GameContext 未初始化则延迟订阅）
        /// </summary>
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (handler == null) return;

            try
            {
                // 如果 GameContext 已初始化，立即订阅
                if (GameContext.IsInitialized)
                {
                    var eventBus = GameContext.Instance.EventBus;
                    eventBus.Subscribe(handler);
                    _subscriptions.Add(new Subscription<TEvent>(eventBus, handler));
                    UnityEngine.Debug.Log($"[EventSubscriberHelper] 已订阅事件: {typeof(TEvent).Name}");
                }
                else
                {
                    // 如果未初始化，先保存 handler，稍后通过 EnsureInitializedAndSubscribe 订阅
                    // 注意：这需要调用者稍后调用 EnsureInitializedAndSubscribe，否则订阅会丢失
                    UnityEngine.Debug.LogWarning($"[EventSubscriberHelper] GameContext 未初始化，已保存订阅请求 {typeof(TEvent).Name}，请稍后调用 EnsureInitializedAndSubscribe");
                    _subscriptions.Add(new PendingSubscription<TEvent>(handler));
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[EventSubscriberHelper] 订阅事件失败 {typeof(TEvent).Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// 确保已初始化并完成所有待处理的订阅
        /// </summary>
        public void EnsureInitializedAndSubscribe()
        {
            if (!GameContext.IsInitialized) return;

            var eventBus = GameContext.Instance.EventBus;
            _eventBus = eventBus;

            // 处理所有待处理的订阅
            for (int i = _subscriptions.Count - 1; i >= 0; i--)
            {
                if (_subscriptions[i] is PendingSubscription pending)
                {
                    var subscription = pending.Subscribe(eventBus);
                    if (subscription != null)
                    {
                        _subscriptions[i] = subscription;
                    }
                    else
                    {
                        _subscriptions.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// 确保已初始化（用于延迟初始化）
        /// </summary>
        public void EnsureInitialized()
        {
            if (_eventBus == null && GameContext.IsInitialized)
            {
                _eventBus = GameContext.Instance.EventBus;
            }
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                try
                {
                    subscription.Dispose();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[EventSubscriberHelper] 取消订阅失败: {ex.Message}");
                }
            }
            _subscriptions.Clear();
            _eventBus = null;
        }

        // 内部接口：可取消的订阅
        private interface IDisposableSubscription : IDisposable { }

        // 抽象基类：待处理的订阅
        private abstract class PendingSubscription : IDisposableSubscription
        {
            public abstract IDisposableSubscription? Subscribe(EventBus eventBus);
            public void Dispose() { } // 待处理的订阅不需要取消
        }

        // 具体类：待处理的类型化订阅
        private class PendingSubscription<TEvent> : PendingSubscription where TEvent : class
        {
            private readonly Action<TEvent> _handler;

            public PendingSubscription(Action<TEvent> handler)
            {
                _handler = handler;
            }

            public override IDisposableSubscription? Subscribe(EventBus eventBus)
            {
                try
                {
                    eventBus.Subscribe(_handler);
                    return new Subscription<TEvent>(eventBus, _handler);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[EventSubscriberHelper] 延迟订阅失败 {typeof(TEvent).Name}: {ex.Message}");
                    return null;
                }
            }
        }

        // 内部类：类型化的订阅
        private class Subscription<TEvent> : IDisposableSubscription where TEvent : class
        {
            private readonly EventBus _eventBus;
            private readonly Action<TEvent> _handler;

            public Subscription(EventBus eventBus, Action<TEvent> handler)
            {
                _eventBus = eventBus;
                _handler = handler;
            }

            public void Dispose()
            {
                _eventBus.Unsubscribe(_handler);
            }
        }
    }
}
