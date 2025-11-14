## DuckyNet.RPC 使用与优化指南

### 1. 目录定位
- `Core/`：通信核心（`RpcClient`、`RpcServer`、`RpcInvoker`、序列化、消息/响应处理、管道等）。
- `Context/`：服务端 `IClientContext` 及其扩展（用于向单个客户端发起 RPC）。
- `Extensions/`：服务器端扩展（例如基于过滤器的广播代理）。
- `Messages/`：装饰服务/方法的属性与消息结构。
- `Utils/`：配置、日志、超时管理。
- `Tools/`：`RpcCodeGenerator` 与命令行入口。

### 2. 消息流概览（Reg + Send）
1. **客户端发起调用**  
   - 业务代码使用 `client.Send<TService>().Method(args)`。  
   - `Send` 代理内部调用 `RpcMessageBuilder` 生成请求，再通过 LiteNetLib `NetPeer.Send` 可靠发送。
2. **服务器接收 + Reg 链执行**  
   - `RpcServer.OnNetworkReceive` 经 `RpcMessageHandler` 反序列化请求。  
   - `RpcInvoker` 根据 `RpcServiceAttribute` 找到服务，逐个执行通过 `server.Reg<TService>().Method(...)` 注册的 handler（支持 `next`）。
3. **服务器回包**  
   - handler 结果或异常由 `RpcMessageBuilder` 封装成 `RpcResponse` 返回客户端。
4. **客户端等待响应**  
   - `RpcClient.HandleMessage` 识别响应，`RpcResponseHandler` 匹配 `messageId` 并完成 `Send` 代理内部的 `Task`.
5. **服务器→客户端推送**  
   - 服务器通过 `context.Send<TService>()`、`server.SendTo<TService>(predicate)` 或 `Broadcast<TService>()` 构造请求下发。  
   - 客户端收到后调用 `client.Reg<TService>().Method(...)` 注册链执行处理逻辑。

### 3. 服务与方法声明
- 通过 `RpcServiceAttribute` 标记接口并定义 ServiceName。
- 使用 `ClientToServerAttribute` / `ServerToClientAttribute` 区分调用方向，代码生成器据此生成发送代理/派发器/注册器。
- 所有 `RpcMethodAttribute` 方法在注册阶段被 `RpcInvoker` 管理，若首参是 `IClientContext` 则自动注入调用者上下文。

### 4. Reg × Send 统一规范

#### 4.1 Send（调用）规范

| 角色 | 写法 | 说明 |
| --- | --- | --- |
| 客户端 → 服务器 | `client.Send<IChatService>().SendMessage(dto)` | `RpcClient.Send<TService>()` 返回强类型代理，内部使用消息构建器 + LiteNetLib 可靠发送。 |
| 服务器 → 指定客户端 | `context.Send<IChatService>().ReceiveMessage(dto)` | `IClientContext.Send<TService>()`（由生成器提供代理）用于单播。 |
| 服务器 → 过滤/列表/广播 | `server.SendTo<IChatService>(predicate).ReceiveMessage(dto)` | `predicate: Func<string,bool>`，也可传入自定义过滤器。便捷方法 `SendTo(clientIds)` / `Broadcast()` 基于同一机制。 |

该规范要求运行 RPC 代码生成器以生成 `*ClientSendProxy` / `*ClientCallProxy` / `*SendProxy`，若缺失会在第一次调用时抛出 “请运行代码生成器” 异常。

#### 4.2 Reg（注册）规范
- **任意位置注册**：客户端与服务器都可以在任何模块通过 `Reg<TService>()` 链式注册方法处理器，不再局限于集中入口。
- **完全链式**：每次调用 `Reg<TService>().MethodName(handler)` 都会向该方法的处理链追加一个 handler，顺序即注册顺序。
- **next 机制**：handler 签名形如 `Func<params..., RpcMethodHandler?, Task<TResult>>`，通过 `next?.Invoke(...)` 继续传递，可实现监控、鉴权、短路等。
- **客户端注册**：`client.Reg<IChatService>().ReceiveMessage(handler)` 处理服务器推送；可在 UI、系统模块等任意位置调用。
- **服务器注册**：`server.Reg<IChatService>().SendMessage(handler)` 处理客户端请求；同样支持多模块拆分。

### 5. 客户端接入流程（Reg + Send 版）
1. **配置与实例化**  
   ```csharp
   var client = new RpcClient(RpcConfig.Development)
       .UseMiddleware(new LoggingMiddleware());
   ```
2. **注册处理器（任意模块）**  
   ```csharp
   client.Reg<IChatService>()
         .ReceiveMessage(async (msg, next) =>
         {
             Console.WriteLine($"UI => {msg.Text}");
             if (next != null)
                 await next(msg, null);
         });
   ```
3. **连接与循环**  
   - `client.Connect(host, port);` 并在主循环 `client.Update();`。
4. **调用服务器**  
   - 无返回值：`client.Send<IChatService>().Ping();`  
   - 有返回值：`var result = await client.Send<IChatService>().SendMessage(msg);`
5. **事件回调**  
   - 订阅 `Connected` / `Disconnected` / `ConnectionFailed`。

### 6. 服务器托管流程（Reg + Send 版）
1. **实例化与启动**  
   ```csharp
   var server = new RpcServer(RpcConfig.Production)
       .UseMiddleware(new AuthorizationMiddleware());
   server.Start(port);
   ```
2. **主循环**  
   - `server.Update();` 轮询网络事件。
3. **任意模块注册请求处理**  
   ```csharp
   server.Reg<IChatService>()
         .SendMessage(async (msg, next) =>
         {
             // 前置验证
             if (string.IsNullOrWhiteSpace(msg.Text))
                 throw new InvalidOperationException("Message empty");

             return next != null ? await next(msg, null) : new SendResult(true, null);
         });
   ```
4. **向客户端推送**  
   - 单个：`context.Send<IChatService>().ReceiveMessage(dto);`  
   - 过滤：`server.SendTo<IChatService>(id => vipIds.Contains(id)).ReceiveMessage(dto);`  
   - 广播：`server.Broadcast<IChatService>().ReceiveMessage(dto);`
5. **注册链扩展**  
   - 可继续通过 `server.Reg<TService>().Method(handler)` 在多个文件堆叠 handler，默认按注册顺序执行，除非 handler 将 `next` 丢弃实现短路。

### 7. 中间件与方法处理链
- **中间件**：实现 `IRpcMiddleware.InvokeAsync`，在 `RpcInvoker.InvokeAsync` 流程中执行，可在 `UseMiddleware` 链式添加。  
  - `LoggingMiddleware`、`AuthorizationMiddleware`、`CacheMiddleware` 展示了日志、权限、缓存三种模式。
- **方法处理器**：`RpcMethodHandler` 支持 `next`，可通过 `RpcMethodHandlerWrapper` 将常规 `Action/Func` 快速包装，实现类似 ASP.NET pipeline 的分层拦截。

### 8. 代码生成器（Tools/RpcCodeGenerator.cs）
- 读取 `Shared` 程序集中的接口，生成：
  - 客户端/服务器派发器与代理。
  - `ClientSendProxy`（客户端 `RpcClient.Send<T>()` 使用）、`ClientCallProxy`（`IClientContext.Send<T>()` 使用）、`SendProxy`（服务器过滤器发送使用）。
  - `Reg`/`ServerReg` 流式注册器。
  - `RpcTypeRegistry` 序列化类型列表（供 `RpcSerializer` 自动装载）。  
- Debug 模式下 `DuckyNet.RPC` 作为可执行入口，`RpcCodeGenTool.Main` 能独立运行生成流程。

### 9. 辅助组件
- **`RpcConfig`**：提供默认/开发/生产预设，涵盖超时、心跳、重连、并发数、日志级别、最大客户端等。
- **`RpcTimeoutManager`**：每个异步调用都会注册超时 token，断线或超时时会统一取消。
- **`RpcLog`**：支持注入自定义 `IRpcLogger`，默认输出到控制台。

### 10. 典型使用清单
1. **定义接口**（Shared 工程）  
   ```csharp
   [RpcService("PlayerService")]
   public interface IPlayerService {
       [ClientToServer] Task<PlayerSnapshot> Sync(IClientContext ctx, PlayerInput input);
       [ServerToClient] void NotifyJoined(PlayerSnapshot snapshot);
   }
   ```
2. **运行代码生成器**：构建 Shared → 运行 `DuckyNet.RPC`（Debug）或调用 `RpcCodeGenerator.GenerateAll`，生成所有 Send 代理。
3. **服务器侧**  
   - 通过 `server.Reg<IPlayerService>().Sync(async (ctx, input, next) => { ... })` 注册处理链。  
   - 推送使用 `server.Broadcast<IPlayerService>().NotifyJoined(snapshot);`
4. **客户端侧**  
   - 在任意模块 `client.Reg<IPlayerService>().NotifyJoined(snapshot => ShowUI(snapshot));`。  
   - 调用服务端：`await client.Send<IPlayerService>().Sync(ctx, input);`

### 10.1 双端完整示例（Send 规范）
以下示例展示了「服务器接收客户端请求 + 服务器主动推送 + 客户端处理回调」的闭环：

1. **Shared 工程：接口与 DTO**
   ```csharp
   [RpcService("ChatService")]
   public interface IChatService
   {
       [ClientToServer]
       Task<SendResult> SendMessage(IClientContext ctx, ChatMessage msg);

       [ServerToClient]
       void ReceiveMessage(ChatMessage msg);
   }

   public record ChatMessage(string SenderId, string Text, DateTime SentAt);
   public record SendResult(bool Ok, string? Error);
   ```

2. **服务器端：Reg 链实现 & 推送**
   ```csharp
   var server = new RpcServer(RpcConfig.Production)
       .UseMiddleware(new LoggingMiddleware());

   server.Reg<IChatService>()
         .SendMessage(async (msg, next) =>
         {
             Console.WriteLine($"[Client] -> {msg.Text}");
             server.Broadcast<IChatService>().ReceiveMessage(msg);
             return next != null ? await next(msg, null) : new SendResult(true, null);
         });

   while (running)
   {
       server.Update();
       Thread.Sleep(10);
   }
   ```

3. **客户端：Reg 回调 & 发送**
   ```csharp
   var client = new RpcClient(RpcConfig.Development)
       .UseMiddleware(new LoggingMiddleware());

   client.Reg<IChatService>()
         .ReceiveMessage(msg =>
         {
             Console.WriteLine($"[Server -> Client] {msg.SenderId}: {msg.Text}");
         });

   client.Connect("127.0.0.1", 9050);

   while (client.ConnectionState == RpcConnectionState.Connecting)
   {
       client.Update();
       Thread.Sleep(10);
   }

   if (client.IsConnected)
   {
       var msg = new ChatMessage("ClientA", "Hello RPC!", DateTime.UtcNow);
       var result = await client.Send<IChatService>().SendMessage(msg);
       Console.WriteLine($"Send result: {result.Ok}");
   }

   while (running)
   {
       client.Update(); // 处理服务器推送 ReceiveMessage
       Thread.Sleep(10);
   }
   ```

### 10. 可进一步优化的方向
1. **线程安全的消息 ID**  
   - `RpcMessageBuilder` 当前使用 `_nextMessageId++`（非线程安全）。在多线程发起 RPC 时存在重复 ID 风险。可改为 `Interlocked.Increment(ref _nextMessageId)` 并在达到上限时回绕。
2. **连接状态/心跳策略**  
   - `RpcClient` 仅在 `Update` 中检测 5 秒连接超时，未实现 `RpcConfig.HeartbeatIntervalMs` 与重连逻辑。建议添加心跳包、自动重连和指数退避，其配置项已经在 `RpcConfig` 中预留。
3. **序列化类型注册健壮性**  
   - `RpcSerializer` 依赖 `DuckyNet.Shared` 中 `RpcTypeRegistry`。若 Shared 尚未编译会回退至基础类型，导致运行期缺少自定义 DTO。可以在启动时检测并给出更清晰的错误提示，或允许手动注入类型集合。
4. **反射调用开销**  
   - `RpcInvoker` 对每次调用都使用 `MethodInfo.Invoke`。可在注册阶段将 `MethodInfo` 编译为 `Func<>`/`Action<>`（Expression Tree 或 DynamicMethod）以减少高频调用开销。
5. **ResponseHandler 资源释放**  
   - 断开连接后仅调用 `_timeoutManager.ClearTimeout`，但 `CancellationTokenSource` Dispose 频率受并发字典影响。可考虑对象池或以 `ValueTask` 返回以降低 GC 压力。
6. **消息管道扩展性**  
   - 当前 `RpcMiddlewarePipeline` 仅支持线性链。可引入命名管道/阶段（如授权、反作弊、速率限制）并提供 middleware ordering / category，以便大型项目组合。
7. **代码生成依赖路径**  
   - 工具默认定位 `Shared/bin/Debug/netstandard2.1`。在 Release 或不同 TFMs 下需要手动传参。可通过读取 `Directory.Build.props` 或 `dotnet msbuild` 查询产物，从而简化 CI/CD。

> 建议将本文档保存为团队 Wiki 或 README 的一部分，后续新增服务/中间件时可直接补充此页。


