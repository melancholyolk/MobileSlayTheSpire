# EventManager 使用指南

`Assets/Scripts/Events` 提供纯 C# 的同步观察者模式，程序集为 `Spire.Events`，命名空间为 `SpireEvents`。使用方在自己的 asmdef 中引用 `Spire.Events`。

一个应用或一局游戏持有一个 `EventManager`，把同一个实例传给需要通信的系统。事件类型就是订阅键，不需要字符串、事件枚举或强制转换；管理器也不依赖场景里的单例对象。

## 定义事件

事件仅携带数据，继承 `EventBase`，提供无参数构造函数，并在 `Reset` 中清空所有字段，尤其是 Unity 对象、集合和业务对象的引用。

```csharp
using SpireEvents;

public sealed class PlayerHealthChanged : EventBase
{
    public int PlayerId;
    public int Health;
    public int MaxHealth;

    protected override void Reset()
    {
        PlayerId = 0;
        Health = 0;
        MaxHealth = 0;
    }
}
```

## 订阅、发布与释放

```csharp
// 应用/会话创建，随后将 events 注入需要通信的系统。
var events = new EventManager();
events.Prewarm<PlayerHealthChanged>(4);

// 监听方在启用时订阅，在禁用或释放时取消。
events.Subscribe<PlayerHealthChanged>(OnPlayerHealthChanged);

// 发布方从池借对象，填写数据，再同步发布。Publish 自动归还。
var change = events.Rent<PlayerHealthChanged>();
change.PlayerId = playerId;
change.Health = health;
change.MaxHealth = maxHealth;
events.Publish(change);

// 发布后 change 已被清空，不能继续读取、保存或再次发布。
events.Unsubscribe<PlayerHealthChanged>(OnPlayerHealthChanged);

// 应用/会话结束，解除全部订阅并释放池内缓存。
events.Dispose();
```

监听方法签名为 `void OnPlayerHealthChanged(PlayerHealthChanged change)`。推荐缓存方法委托或直接使用方法组；反复创建捕获局部变量的 lambda 会产生调用方自己的分配。

如果借出后取消发布，调用 `events.Return(change)`。`Publish` 即使遇到监听异常也会归还，因此不要在 `Publish` 后再调用 `Return`。事件数据准备阶段可能抛异常时，在准备阶段的 catch 中归还，再抛出；发布应放在该 try/catch 之外，避免重复归还。

## 派发约定

- 仅在同一线程使用，Unity 项目应在主线程调用；网络工作线程先把通知排到主线程。
- 监听按订阅顺序调用；同一委托重复订阅只有一份。取消订阅立即生效，尚未执行的监听在当前派发中会被跳过。
- 同类型事件派发期间的新订阅，在该类型最外层派发结束后生效，包含嵌套派发。可以在回调中取消自己或其他监听。
- 可以嵌套发布，但必须重新 `Rent` 一个事件；正在派发的实例不能再次发布或归还。
- 回调抛异常时，其余有效监听仍会执行；完成清理后重新抛出首个异常并保留原始堆栈。`Reset` 应只清理数据、不能抛异常；若抛出，该实例会被丢弃，Reset 异常会向调用方传播。
- 监听者只在回调期间借用事件。异步任务需要把所需字段复制到自己的状态中，不能持有池对象。
- 管理器销毁后拒绝新租借、订阅和发布；已借出的事件仍可 `Return` 清理。派发过程中不允许销毁管理器，应等当前调用返回。
- 手动 `new` 的事件、其他管理器的事件、重复归还和重复发布都会被拒绝。旧引用在事件再次租出后无法辨别新租约，因此遵守“发布后不再持有实例”的约定是必要的。

## 池与分配

每种事件有独立的对象池。默认每种类型最多缓存 32 个实例，可通过 `new EventManager(maxPooledEventsPerType: 64)` 调整。容量限制的是缓存数量，借出数量超过缓存时仍可创建实例，归还时超出的实例清空后交给 GC。

`Prewarm<T>(count)` 将池内可用实例补足到 count，便于在加载阶段完成分配；需要嵌套发布时按最大并发深度预热。稳定的 Rent → Publish → Reset → Return 路径不创建事件、不复制监听数组，也不产生包装委托。首次注册类型、池耗尽、订阅列表增长以及异常路径仍可能分配；监听者自身的代码也可能分配。

`EventManagerTests.WarmedPublishLoopDoesNotAllocateManagedMemory` 测量预热后 1000 次发布的线程托管分配。其余测试覆盖事件复用、字段清理、监听变更、嵌套、异常、所有权、缓存上限和销毁。

## 接入现有模块

规则仍通过 `Run.Apply` / `Run.ResolveAttack` 修改状态。在应用协调层确认操作成功后，可向 UI、音频或其他系统发布领域通知。事件总线本身不负责网络传输、异步调度、历史保存或回放。

现有 `BattleCue` 是有序表现数据、`CommandRecord` 是联机重放数据，都需要在当前同步回调之外读取，不能直接换成自动归还的池事件。本次新增的是可独立接入的基础设施，现有流程没有迁移到事件总线。
