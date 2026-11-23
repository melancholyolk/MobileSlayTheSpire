using System;
using System.Collections.Generic;

namespace SpireEvents
{
    /// <summary>
    /// 单线程、同步的强类型事件总线。每种事件拥有独立的监听列表和对象池。
    /// 由应用或会话持有并注入需要通信的系统，在所属生命周期结束时 Dispose。
    /// </summary>
    public sealed class EventManager : IDisposable
    {
        readonly Dictionary<Type, IEventChannel> channels = new Dictionary<Type, IEventChannel>();
        readonly int maxPooledEventsPerType;
        int activePublishes;
        bool disposed;

        public EventManager(int maxPooledEventsPerType = 32)
        {
            if (maxPooledEventsPerType < 1)
                throw new ArgumentOutOfRangeException(nameof(maxPooledEventsPerType));

            this.maxPooledEventsPerType = maxPooledEventsPerType;
        }

        /// <summary>预创建可复用实例。count 为该类型池中希望保留的数量，不是追加数量。</summary>
        public void Prewarm<T>(int count = 1) where T : EventBase, new()
        {
            ThrowIfDisposed();
            if (count < 0 || count > maxPooledEventsPerType)
                throw new ArgumentOutOfRangeException(nameof(count));

            GetChannel<T>().Prewarm(count);
        }

        /// <summary>同一回调重复注册只保留一份。派发期间新增的监听在最外层派发结束后生效。</summary>
        public void Subscribe<T>(Action<T> listener) where T : EventBase, new()
        {
            ThrowIfDisposed();
            if (listener == null)
                throw new ArgumentNullException(nameof(listener));

            GetChannel<T>().Subscribe(listener);
        }

        /// <summary>立即取消；尚未轮到的监听者在当前派发中也不会再被调用。</summary>
        public void Unsubscribe<T>(Action<T> listener) where T : EventBase, new()
        {
            if (listener == null)
                throw new ArgumentNullException(nameof(listener));

            if (channels.TryGetValue(typeof(T), out var channel))
                ((EventChannel<T>)channel).Unsubscribe(listener);
        }

        /// <summary>仅池为空时创建实例。填写数据后调用 Publish，放弃发布时调用 Return。</summary>
        public T Rent<T>() where T : EventBase, new()
        {
            ThrowIfDisposed();
            return GetChannel<T>().Rent();
        }

        /// <summary>
        /// 同步通知全部有效监听者并自动归还事件；回调异常在其余监听完成后重新抛出首个异常。
        /// 嵌套发布必须再次 Rent，不能重复发布当前正在派发的实例。
        /// </summary>
        public void Publish(EventBase eventData)
        {
            ThrowIfDisposed();
            ValidateLease(eventData);
            eventData.State = EventState.Publishing;
            activePublishes++;
            try
            {
                // 使用租借时绑定的通道，避免按调用处的基类类型路由到错误的监听列表。
                eventData.Channel.Dispatch(eventData);
            }
            finally
            {
                try
                {
                    eventData.Channel.Return(eventData);
                }
                finally
                {
                    activePublishes--;
                }
            }
        }

        /// <summary>归还尚未发布的租借。管理器已销毁时也允许归还，以清除事件数据。</summary>
        public void Return(EventBase eventData)
        {
            ValidateLease(eventData);
            eventData.Channel.Return(eventData);
        }

        EventChannel<T> GetChannel<T>() where T : EventBase, new()
        {
            if (!channels.TryGetValue(typeof(T), out var channel))
            {
                channel = new EventChannel<T>(this, maxPooledEventsPerType);
                channels.Add(typeof(T), channel);
            }

            return (EventChannel<T>)channel;
        }

        void ValidateLease(EventBase eventData)
        {
            if (eventData == null)
                throw new ArgumentNullException(nameof(eventData));
            if (eventData.Owner != this || eventData.State != EventState.Rented)
                throw new InvalidOperationException("Event must be an outstanding rental from this manager.");
        }

        void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(EventManager));
        }

        public void Dispose()
        {
            if (disposed)
                return;
            if (activePublishes != 0)
                throw new InvalidOperationException("Dispose the event manager after publishing has finished.");

            disposed = true;
            foreach (var channel in channels.Values)
                channel.Dispose();
            channels.Clear();
        }
    }
}
