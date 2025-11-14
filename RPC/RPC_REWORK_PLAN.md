## RPC 重构任务总览

> 目标：落地 “Send + Reg” 新规范，彻底移除旧有 `Invoke*/RegisterService` API，并与 Shared 生成物保持一致。

### 任务 1：接口 & DTO 调整（Shared/Services/）
- 所有 `[ClientToServer]` 方法去除 `IClientContext` 参数，必要的调用者信息由运行时注入。
- `[ServerToClient]` 方法保持原样，如需上下文由 `Reg` handler 参数提供。
- 扫描 `Services/` 目录内 13 个接口：`IPlayerService`、`IRoomService` 等；同步更新相关 DTO 注释说明，强调客户端无需传入上下文。

### 任务 2：核心运行时改造（RPC/Core）
1. **RpcClient**
   - 新增 `Send<TService>()`：从生成的 `ClientSendProxy` 创建强类型代理，缓存实例。
   - 新增 `Reg<TService>()`：提供 `Reg` 入口，内部调用 `_invoker.RegisterMethodHandler`，同时支持多处注册与 `next`.
   - 移除或标记废弃 `InvokeServer*` 与 `RegisterClientService`.
2. **RpcServer**
   - 新增 `Reg<TService>()`，供服务端按接口方法注册 handler。
   - `ServerClientContext.Send<T>()`、`RpcServer.SendTo/Broadcast` 继续存在，但依赖新的 `SendProxy`.
   - `InvokeClient*` 继续提供给生成代理内部使用，但不暴露给业务层。
3. **RpcInvoker**
   - Handler 签名统一为 `(params..., IClientContext? ctx, RpcMethodHandler? next)`。
   - 默认中间件负责注入 `context.ClientContext`，并将返回值写入 `RpcMiddlewareContext.Result`.
4. **RpcMiddlewareContext**
   - 扩展属性：`RawParameters`、`HandlerChain`.
5. **兼容策略**
   - 提供编译期 `Obsolete` 警告，引导迁移。

### 任务 3：代码生成器（RPC/Core/RpcCodeGenerator.cs）
- 生成以下产物（按服务接口）：
  1. `*ClientSendProxy`：用于 `RpcClient.Send<T>`.
  2. `*ClientCallProxy`：用于 `IClientContext.Send<T>`.
  3. `*SendProxy`：`RpcServer.SendTo<T>` 多播代理。
  4. `*Reg` / `*ServerReg` 链式注册器，符合文档示例（任意模块调用、支持 next）。
- 去掉旧的 `*ClientProxy` / `MiddlewareExtensions`.
- 生成 `RpcTypeRegistry`.

### 任务 4：Server/Client 业务迁移
- **Server**：将 `Server/Services/*Impl.cs` 中的接口实现改为 `server.Reg<TService>()` 形式，拆分至对应模块；删除旧实现类或改为注册器。
- **Client**：在需要接收服务器推送的模块中，使用 `client.Reg<TService>()` 注册 handler；发送调用统一改为 `client.Send<TService>()`.

### 任务 5：Shared/Generated 重生成
- 运行更新后的 `RpcCodeGenerator`，清理 `Shared/Generated/` 旧文件后重新生成。
- 验证生成物包含新的 proxy/reg 文件结构。

### 任务 6：测试与验证
- 编译 Shared、RPC、Server、Client，确保无旧 API 调用。
- 编写基础连通性测试：客户端发送请求、服务器 handler 返回、服务器推送广播。
- 关注序列化类型是否完整（自动类型注册是否覆盖新的 DTO）。

### 时间建议
1. 设计与接口调整（任务 1） —— 0.5 天
2. Core 改造（任务 2） —— 1.5 天
3. 代码生成器重写（任务 3） —— 1 天
4. 业务迁移（任务 4） —— 1 天
5. 生成 & 测试（任务 5/6） —— 0.5 天

> 完成后再更新 `RPC_USAGE.md` 细节（或整合到最终文档）。


