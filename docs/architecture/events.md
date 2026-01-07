# 事件边界与分类约定

## 事件边界

- **RPC 事件**：仅负责“跨网络”的输入输出（客户端 ↔ 服务器）。
- **EventBus 事件**：仅负责“进程内”的发布与订阅（同一进程内部模块解耦）。

> 规则：任何跨网络的数据只能通过 RPC 传输；任何进程内的解耦通知只能通过 EventBus 发布/订阅。

## 领域事件定义

- **统一接口**：所有领域事件都实现 `DuckyNet.Shared.Events.IEvent`。
- **统一基类**：默认继承 `DuckyNet.Shared.Events.EventBase`，由基类提供 `Name` 与 `OccurredAt`。

## 命名规范

- 事件类型使用 **PascalCase**，并以 `Event` 结尾（例如：`PlayerJoinedEvent`）。
- 事件类型命名反映业务语义，而非传输细节。

## 网络 → 领域事件转换

- **服务层（RPC）是唯一的翻译入口**：
  - 服务器端 `Server/Services/*ServiceImpl.cs`：接收 RPC 请求 → 解析为 `IEvent` → 发布到服务器 `EventBus`。
  - 客户端 `Client/Services/*ClientServiceImpl.cs`：接收 RPC 回调 → 解析为 `IEvent` → 发布到客户端 `EventBus`。
- 业务模块只订阅 `EventBus` 事件，不直接依赖 RPC 层。

## 重复事件链清理原则

- **RPC 只负责网络输入输出**，不在业务逻辑中重复发布/订阅事件。
- **EventBus 只负责进程内分发**，事件消费端不直接调用 RPC。

## EventBus 注册约定

- 服务器仅注册一个 `DuckyNet.Server.Events.EventBus` 实例。
- 统一通过 `Server/Core/ServiceCollectionExtensions.cs` 中的 `AddDuckyNetCore()` 注册。
