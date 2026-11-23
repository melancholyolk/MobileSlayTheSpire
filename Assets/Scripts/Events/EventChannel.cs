using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace SpireEvents
{
    internal interface IEventChannel : IDisposable
    {
        void Dispatch(EventBase eventData);
        void Return(EventBase eventData);
    }

    /// <summary>每种事件只创建一个通道；稳定派发路径不复制监听数组、不创建包装委托。</summary>
    internal sealed class EventChannel<T> : IEventChannel where T : EventBase, new()
    {
        readonly EventManager owner;
        readonly int capacity;
        readonly Stack<T> pool;
        readonly List<Action<T>> listeners = new List<Action<T>>(8);
        readonly List<Action<T>> pendingListeners = new List<Action<T>>(4);
        int dispatchDepth;
        bool disposed;

        internal EventChannel(EventManager owner, int capacity)
        {
            this.owner = owner;
            this.capacity = capacity;
            pool = new Stack<T>(capacity);
        }

        internal void Prewarm(int count)
        {
            while (pool.Count < count)
            {
                var eventData = Create();
                Return(eventData);
            }
        }

        T Create()
        {
            return new T { Owner = owner, Channel = this };
        }

        internal T Rent()
        {
            var eventData = pool.Count == 0 ? Create() : pool.Pop();
            eventData.State = EventState.Rented;
            return eventData;
        }

        internal void Subscribe(Action<T> listener)
        {
            if (listeners.Contains(listener) || pendingListeners.Contains(listener))
                return;

            if (dispatchDepth == 0)
                listeners.Add(listener);
            else
                pendingListeners.Add(listener);
        }

        internal void Unsubscribe(Action<T> listener)
        {
            pendingListeners.Remove(listener);
            int index = listeners.IndexOf(listener);
            if (index < 0)
                return;

            if (dispatchDepth == 0)
                listeners.RemoveAt(index);
            else
                listeners[index] = null;
        }

        public void Dispatch(EventBase eventData)
        {
            Exception firstError = null;
            dispatchDepth++;
            try
            {
                int count = listeners.Count;
                for (int i = 0; i < count; i++)
                {
                    var listener = listeners[i];
                    if (listener == null)
                        continue;

                    try
                    {
                        listener((T)eventData);
                    }
                    catch (Exception error)
                    {
                        if (firstError == null)
                            firstError = error;
                    }
                }
            }
            finally
            {
                dispatchDepth--;
                if (dispatchDepth == 0)
                    ApplyListenerChanges();
            }

            if (firstError != null)
                ExceptionDispatchInfo.Capture(firstError).Throw();
        }

        void ApplyListenerChanges()
        {
            // 派发期间保留索引；最外层返回后原地压缩，避免每次发布分配快照。
            int write = 0;
            for (int read = 0; read < listeners.Count; read++)
                if (listeners[read] != null)
                    listeners[write++] = listeners[read];

            if (write < listeners.Count)
                listeners.RemoveRange(write, listeners.Count - write);
            for (int i = 0; i < pendingListeners.Count; i++)
                listeners.Add(pendingListeners[i]);
            pendingListeners.Clear();
        }

        public void Return(EventBase eventData)
        {
            eventData.State = EventState.Returning;
            try
            {
                eventData.ResetForPool();
            }
            catch
            {
                // Reset 失败的脏数据不能再次进入池。
                Detach(eventData);
                throw;
            }

            eventData.State = EventState.Available;
            if (!disposed && pool.Count < capacity)
                pool.Push((T)eventData);
            else
                Detach(eventData);
        }

        static void Detach(EventBase eventData)
        {
            eventData.State = EventState.Available;
            eventData.Owner = null;
            eventData.Channel = null;
        }

        public void Dispose()
        {
            disposed = true;
            listeners.Clear();
            pendingListeners.Clear();
            while (pool.Count > 0)
                Detach(pool.Pop());
        }
    }
}
