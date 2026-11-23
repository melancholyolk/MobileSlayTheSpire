using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpireEvents;

namespace Spire.Tests
{
    public class EventManagerTests
    {
        public sealed class ValueEvent : EventBase
        {
            public int Value;
            public object Context;

            protected override void Reset()
            {
                Value = 0;
                Context = null;
            }
        }

        public sealed class OtherEvent : EventBase
        {
            protected override void Reset() { }
        }

        public sealed class BrokenResetEvent : EventBase
        {
            protected override void Reset()
            {
                throw new InvalidOperationException("Reset failed");
            }
        }

        [Test]
        public void PublishUsesRentedTypeAndReusesClearedInstance()
        {
            using (var events = new EventManager())
            {
                events.Prewarm<ValueEvent>();
                int observed = 0;
                events.Subscribe<ValueEvent>(e => observed = e.Value);
                events.Subscribe<OtherEvent>(e => Assert.Fail("Wrong event type"));
                var value = events.Rent<ValueEvent>();
                value.Value = 42;
                value.Context = new object();
                EventBase baseReference = value;
                events.Publish(baseReference);

                Assert.AreEqual(42, observed);
                var reused = events.Rent<ValueEvent>();
                Assert.AreSame(value, reused);
                Assert.AreEqual(0, reused.Value);
                Assert.IsNull(reused.Context);
                events.Return(reused);
            }
        }

        [Test]
        public void DuplicateSubscriptionAndUnsubscriptionAreIdempotent()
        {
            using (var events = new EventManager())
            {
                int calls = 0;
                Action<ValueEvent> listener = e => calls++;
                events.Subscribe(listener);
                events.Subscribe(listener);
                events.Publish(events.Rent<ValueEvent>());
                Assert.AreEqual(1, calls);
                events.Unsubscribe(listener);
                events.Unsubscribe(listener);
                events.Publish(events.Rent<ValueEvent>());
                Assert.AreEqual(1, calls);
            }
        }

        [Test]
        public void ListenerChangesDuringDispatchHaveStableOrder()
        {
            using (var events = new EventManager())
            {
                var calls = new List<string>();
                Action<ValueEvent> added = e => calls.Add("added");
                Action<ValueEvent> removed = e => calls.Add("removed");
                Action<ValueEvent> first = null;
                first = e =>
                {
                    calls.Add("first");
                    events.Unsubscribe(first);
                    events.Unsubscribe(removed);
                    events.Subscribe(added);
                };
                events.Subscribe(first);
                events.Subscribe(removed);
                events.Publish(events.Rent<ValueEvent>());
                CollectionAssert.AreEqual(new[] { "first" }, calls);
                events.Publish(events.Rent<ValueEvent>());
                CollectionAssert.AreEqual(new[] { "first", "added" }, calls);
            }
        }

        [Test]
        public void NestedPublishKeepsOuterDataAndDefersNewListeners()
        {
            using (var events = new EventManager())
            {
                events.Prewarm<ValueEvent>(2);
                var seen = new List<int>();
                int lateCalls = 0;
                Action<ValueEvent> late = e => lateCalls++;
                events.Subscribe<ValueEvent>(outer =>
                {
                    if (outer.Value != 1)
                        return;
                    events.Subscribe(late);
                    var nested = events.Rent<ValueEvent>();
                    Assert.AreNotSame(outer, nested);
                    nested.Value = 2;
                    events.Publish(nested);
                    Assert.AreEqual(1, outer.Value);
                });
                events.Subscribe<ValueEvent>(e => seen.Add(e.Value));
                var value = events.Rent<ValueEvent>();
                value.Value = 1;
                events.Publish(value);
                CollectionAssert.AreEqual(new[] { 2, 1 }, seen);
                Assert.AreEqual(0, lateCalls);
                events.Publish(events.Rent<ValueEvent>());
                Assert.AreEqual(1, lateCalls);
            }
        }

        [Test]
        public void ListenerExceptionDoesNotSkipOthersOrLeakTheEvent()
        {
            using (var events = new EventManager())
            {
                var failure = new InvalidOperationException("Listener failed");
                int observed = 0;
                events.Subscribe<ValueEvent>(e => throw failure);
                events.Subscribe<ValueEvent>(e => observed = e.Value);
                var value = events.Rent<ValueEvent>();
                value.Value = 9;
                Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() => events.Publish(value)));
                Assert.AreEqual(9, observed);
                var reused = events.Rent<ValueEvent>();
                Assert.AreSame(value, reused);
                Assert.AreEqual(0, reused.Value);
                events.Return(reused);
            }
        }

        [Test]
        public void InvalidOwnershipAndDoubleReturnAreRejected()
        {
            using (var owner = new EventManager())
            using (var other = new EventManager())
            {
                var value = owner.Rent<ValueEvent>();
                Assert.Throws<InvalidOperationException>(() => other.Publish(value));
                Assert.Throws<InvalidOperationException>(() => other.Return(value));
                Assert.Throws<InvalidOperationException>(() => owner.Publish(new ValueEvent()));
                owner.Return(value);
                Assert.Throws<InvalidOperationException>(() => owner.Return(value));
                Assert.Throws<InvalidOperationException>(() => owner.Publish(value));
            }
        }

        [Test]
        public void PublishingInstanceCannotBeReturnedRepublishedOrDisposed()
        {
            using (var events = new EventManager())
            {
                events.Subscribe<ValueEvent>(e =>
                {
                    Assert.Throws<InvalidOperationException>(() => events.Return(e));
                    Assert.Throws<InvalidOperationException>(() => events.Publish(e));
                    Assert.Throws<InvalidOperationException>(() => events.Dispose());
                });
                var value = events.Rent<ValueEvent>();
                events.Publish(value);
                var reused = events.Rent<ValueEvent>();
                Assert.AreSame(value, reused);
                events.Return(reused);
            }
        }

        [Test]
        public void PoolCapacityBoundsRetentionAndClearsOverflow()
        {
            using (var events = new EventManager(1))
            {
                var first = events.Rent<ValueEvent>();
                var overflow = events.Rent<ValueEvent>();
                overflow.Context = new object();
                events.Return(first);
                events.Return(overflow);
                Assert.IsNull(overflow.Context);
                var retained = events.Rent<ValueEvent>();
                var fresh = events.Rent<ValueEvent>();
                Assert.AreSame(first, retained);
                Assert.AreNotSame(overflow, fresh);
                events.Return(retained);
                events.Return(fresh);
            }
        }

        [Test]
        public void DisposeReleasesListenersAndAllowsOutstandingRentalCleanup()
        {
            var events = new EventManager();
            Action<ValueEvent> listener = e => Assert.Fail("Disposed listener");
            events.Subscribe(listener);
            var outstanding = events.Rent<ValueEvent>();
            outstanding.Context = new object();
            events.Dispose();
            events.Dispose();
            events.Unsubscribe(listener);
            Assert.Throws<ObjectDisposedException>(() => events.Rent<ValueEvent>());
            Assert.Throws<ObjectDisposedException>(() => events.Subscribe(listener));
            Assert.Throws<ObjectDisposedException>(() => events.Publish(outstanding));
            events.Return(outstanding);
            Assert.IsNull(outstanding.Context);
        }

        [Test]
        public void FailedResetDiscardsInstanceAndDoesNotPoisonManager()
        {
            using (var events = new EventManager())
            {
                var broken = events.Rent<BrokenResetEvent>();
                Assert.Throws<InvalidOperationException>(() => events.Publish(broken));
                Assert.Throws<InvalidOperationException>(() => events.Return(broken));
                var next = events.Rent<BrokenResetEvent>();
                Assert.AreNotSame(broken, next);
                Assert.Throws<InvalidOperationException>(() => events.Return(next));
                events.Publish(events.Rent<ValueEvent>());
            }
        }

        [Test]
        public void InvalidArgumentsAreRejectedBeforeCreatingLeases()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventManager(0));
            using (var events = new EventManager(2))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => events.Prewarm<ValueEvent>(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => events.Prewarm<ValueEvent>(3));
                Assert.Throws<ArgumentNullException>(() => events.Subscribe<ValueEvent>(null));
                Assert.Throws<ArgumentNullException>(() => events.Unsubscribe<ValueEvent>(null));
                Assert.Throws<ArgumentNullException>(() => events.Publish(null));
                Assert.Throws<ArgumentNullException>(() => events.Return(null));
            }
        }

        [Test]
        public void WarmedPublishLoopDoesNotAllocateManagedMemory()
        {
            using (var events = new EventManager())
            {
                int total = 0;
                events.Subscribe<ValueEvent>(e => total += e.Value);
                events.Prewarm<ValueEvent>(2);
                for (int i = 0; i < 32; i++)
                    events.Publish(events.Rent<ValueEvent>());

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++)
                {
                    var value = events.Rent<ValueEvent>();
                    value.Value = 1;
                    events.Publish(value);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.AreEqual(1000, total);
                Assert.AreEqual(0, allocated, "The warmed rent/publish/return path should not allocate.");
            }
        }
    }
}
