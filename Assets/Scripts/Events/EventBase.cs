namespace SpireEvents
{
    /// <summary>
    /// 可复用的事件数据。通过 EventManager.Rent 获取，Publish 后自动归还。
    /// 监听者只能在回调期间读取，不能保存实例或提前归还。
    /// </summary>
    public abstract class EventBase
    {
        internal EventManager Owner;
        internal IEventChannel Channel;
        internal EventState State;

        /// <summary>归还前清空所有数据和对象引用；实现应无副作用且不抛出异常。</summary>
        protected abstract void Reset();

        internal void ResetForPool()
        {
            Reset();
        }
    }

    internal enum EventState
    {
        Available,
        Rented,
        Publishing,
        Returning
    }
}
