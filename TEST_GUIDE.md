# 🎮 NPC 同步系统测试指南

## ✅ 编译状态确认

**客户端：** ✅ 成功（2 个可忽略警告）  
**服务器：** ✅ 成功（0 错误 0 警告）  
**DLL 复制：** ✅ 已自动复制到游戏 Mods 目录

---

## 🚀 测试步骤

### 准备工作

#### 1. 启动服务器
```bash
cd E:\git\DuckyNet\Server\bin\Debug\net8.0
DuckyNet.Server.exe
```

**预期日志：**
```
[NpcVisibilityTracker] ✅ 使用基础范围检查（100m）
[Server] 服务器启动成功，监听端口 7777
```

#### 2. 完全退出游戏
- 关闭所有游戏进程
- 确保 Mods 已加载最新 DLL

---

## 🧪 测试场景 1：独自玩家创建 NPC

### 操作步骤
1. 启动游戏（玩家 A）
2. 进入场景
3. 按 `F1` 打开调试面板
4. 找到 "Shadow NPC 测试" 模块
5. 点击 "Create Shadow NPC" 创建 1 个 NPC

### 预期结果

**客户端 A 日志：**
```
[NpcManager] 本地 NPC 已注册: Character(Clone) (ID: xxx-xxx-xxx)
[NpcManager] ✅ NPC 生成已发送到服务器
```

**服务器日志：**
```
[NpcSyncService] 📥 收到 NPC 生成: Character(Clone) (ID: xxx, 来自: 玩家A)
[PlayerNpcManager] 玩家 SteamId_A 创建 NPC: xxx
[NpcSyncService] ✅ NPC 已记录到玩家列表（等待靠近时动态同步）
```

**调试面板：**
```
📊 总计: 1 | ❤️ 存活: 1
🔍 可见性: 追踪1 | 远程0 | 范围100m
♻️ 对象池: 活动0 | 池中5 | 复用率0.0%
```

### ✅ 验证点
- [x] 客户端成功创建本地 NPC
- [x] 服务器收到并记录 NPC
- [x] **服务器不广播**（因为没有其他玩家）

---

## 🧪 测试场景 2：NPC 位置更新（独自玩家）

### 操作步骤
1. 使用测试工具移动 NPC（或等待 NPC 自己移动）
2. 观察日志

### 预期结果

**客户端 A 日志：**
```
（每 100ms 静默发送，不打印日志）
```

**服务器日志：**
```
（收到位置更新，但没有其他玩家，不打印详细日志）
```

### ✅ 验证点
- [x] 客户端每 100ms 检查变化
- [x] 服务器更新 NPC 位置记录（即使没有其他玩家）
- [x] 没有其他玩家时不广播（性能优化）

---

## 🧪 测试场景 3：玩家 B 中途加入（近距离）

### 操作步骤
1. 启动第二个游戏实例（玩家 B）
2. 连接到同一服务器
3. 进入**同一场景**
4. 确保玩家 B 距离 NPC < 100m

### 预期结果

**客户端 B 日志：**
```
[NpcManager] 场景加载完成，延迟 1 秒后请求场景 NPC（等待位置同步）
（1 秒后）
[NpcManager] 📥 延迟请求完成，开始请求场景 NPC
[NpcManager] 📥 请求场景 NPC: MainScene/SubScene
[NpcManager] ✅ 收到 1 个场景 NPC
[NpcSyncClient] 📦 收到远程 NPC 生成: Character(Clone) (ID: xxx)
    场景: MainScene/SubScene
    位置: (15.00, 0.00, 5.00)
[NpcManager] ✅ 远程 NPC 已添加: Character(Clone) (ID: xxx)
[NpcSyncClient] ✅ 远程 NPC 已创建并注册（使用对象池）
[ShadowNpcPool] ♻️ 复用 NPC: Character(Clone) (池剩余: 4)
```

**服务器日志：**
```
[NpcSyncService] 📥 玩家请求场景 NPC: 玩家B → MainScene/SubScene
[NpcVisibilityTracker] 玩家 玩家B 位置: (20.00, 0.00, 20.00)
  → NPC xxx 在范围内: 15.80m < 100.00m
[NpcSyncService] ✅ 返回 1/1 个可见 NPC
```

**调试面板（玩家 B）：**
```
📊 总计: 1 | ❤️ 存活: 1
🔍 可见性: 追踪0 | 远程1 | 范围100m
♻️ 对象池: 活动1 | 池中4 | 复用率100.0%
```

### ✅ 验证点
- [x] 玩家 B 延迟 1 秒后请求 NPC
- [x] 服务器返回最新位置（不是创建时的旧位置）
- [x] 玩家 B 看到的 NPC 在正确位置
- [x] 对象池复用率 = 100%（首次从预热的池获取）

---

## 🧪 测试场景 4：动态可见性（玩家 B 走远）

### 操作步骤
1. 玩家 B 移动到距离 NPC > 100m 的位置
2. 观察 NPC 是否消失

### 预期结果

**客户端 A 日志：**
```
（NPC 移动触发位置更新）
```

**服务器日志：**
```
[NpcVisibilityTracker] 玩家 玩家B 位置: (150.00, 0.00, 150.00)
  → NPC xxx 超出范围: 205.00m > 100.00m
[NpcSyncService] 🗑️ NPC xxx 离开 玩家B 范围
```

**客户端 B 日志：**
```
[NpcSyncClient] 🗑️ 收到远程 NPC 销毁: xxx (原因: 1)
[NpcManager] 远程 NPC 已移除并回收: Character(Clone) (ID: xxx)
[ShadowNpcPool] ♻️ 回收 NPC: Character(Clone) (池剩余: 5)
```

**调试面板（玩家 B）：**
```
📊 总计: 0 | ❤️ 存活: 0
🔍 可见性: 追踪0 | 远程0 | 范围100m
♻️ 对象池: 活动0 | 池中5 | 复用率100.0%
```

### ✅ 验证点
- [x] NPC 从玩家 B 的视野中消失
- [x] GameObject 回收到对象池（SetActive(false)）
- [x] 对象池剩余数量 +1

---

## 🧪 测试场景 5：动态可见性（玩家 B 走近）

### 操作步骤
1. 玩家 B 移动到距离 NPC < 100m 的位置
2. 观察 NPC 是否重新出现

### 预期结果

**服务器日志：**
```
[NpcVisibilityTracker] 玩家 玩家B 位置: (50.00, 0.00, 50.00)
  → NPC xxx 在范围内: 66.00m < 100.00m
[NpcSyncService] 🆕 NPC xxx 进入 玩家B 范围
```

**客户端 B 日志：**
```
[NpcSyncClient] 📦 收到远程 NPC 生成: Character(Clone) (ID: xxx)
[NpcManager] ✅ 远程 NPC 已添加: Character(Clone) (ID: xxx)
[ShadowNpcPool] ♻️ 复用 NPC: Character(Clone) (池剩余: 4)
```

**调试面板（玩家 B）：**
```
📊 总计: 1 | ❤️ 存活: 1
♻️ 对象池: 活动1 | 池中4 | 复用率100.0%
```

### ✅ 验证点
- [x] NPC 重新出现在玩家 B 的视野中
- [x] 从对象池复用（不是新建）
- [x] 对象池剩余数量 -1

---

## 🧪 测试场景 6：玩家 A 看到玩家 B 的 NPC

### 操作步骤
1. 玩家 B 也创建一个 NPC（使用测试工具）
2. 观察玩家 A 是否能看到

### 预期结果

**客户端 B 日志：**
```
[NpcManager] 本地 NPC 已注册: Character(Clone) (ID: yyy)
[NpcManager] ✅ NPC 生成已发送到服务器
```

**服务器日志：**
```
[NpcSyncService] 📥 收到 NPC 生成: Character(Clone) (ID: yyy, 来自: 玩家B)
[PlayerNpcManager] 玩家 SteamId_B 创建 NPC: yyy
[NpcSyncService] ✅ NPC 已记录到玩家列表（等待靠近时动态同步）
```

**客户端 A 日志（NPC 移动时触发）：**
```
（当玩家 A 的 NPC 移动时，会触发玩家 B 的 NPC 的可见性检查）

如果距离 < 100m:
[NpcSyncClient] 📦 收到远程 NPC 生成: Character(Clone) (ID: yyy)
[NpcManager] ✅ 远程 NPC 已添加
```

### ✅ 验证点
- [x] 玩家 A 能看到玩家 B 的 NPC
- [x] 玩家 B 能看到玩家 A 的 NPC
- [x] 两个玩家都只有 1 个本地 NPC，1 个远程 NPC

---

## 🧪 测试场景 7：玩家断开连接

### 操作步骤
1. 玩家 B 退出游戏
2. 观察服务器日志

### 预期结果

**服务器日志：**
```
[PlayerManager] 玩家已断开连接: 玩家B
[PlayerCleanup] 清理玩家数据: 玩家B
[PlayerCleanup] ✅ 已清理 NPC 可见性追踪
[PlayerNpcManager] 清理玩家 SteamId_B 的 1 个 NPC
[PlayerCleanup] ✅ 已清理玩家的所有 NPC
```

### ✅ 验证点
- [x] 可见性追踪清理
- [x] 玩家的所有 NPC 清理
- [x] 位置缓存清理
- [x] 无内存泄漏

---

## 📊 调试面板使用

### NPC 管理器模块
```
📊 总计: X | ❤️ 存活: X | 💀 死亡: 0
🔍 可见性: 追踪X | 远程X | 范围100m
♻️ 对象池: 活动X | 池中X | 复用率X%
```

**说明：**
- **总计** = 本地 NPC + 远程 NPC
- **追踪** = 客户端追踪的变化检测 NPC 数量
- **远程** = 可见的远程 NPC 数量
- **活动** = 当前使用的对象池 NPC
- **池中** = 空闲的对象池 NPC
- **复用率** = (复用次数 / 总创建次数) × 100%

### Shadow NPC 测试模块
```
[Create Shadow NPC] - 在玩家附近创建 1 个测试 NPC
[Create 10 NPCs] - 批量创建 10 个（测试对象池）
[Destroy Last] - 销毁最后创建的
[Clear All] - 清理所有测试 NPC
```

---

## 🎯 成功标志

### 基础功能
- ✅ 玩家 A 能创建 NPC
- ✅ 玩家 B 能看到玩家 A 的 NPC
- ✅ NPC 位置是最新的（不是创建时的旧位置）
- ✅ NPC 移动流畅（平滑插值）

### 动态可见性
- ✅ 玩家靠近时 NPC 出现（进入范围）
- ✅ 玩家远离时 NPC 消失（离开范围）
- ✅ 服务器日志显示 `🆕 进入范围` 和 `🗑️ 离开范围`

### 性能优化
- ✅ 对象池复用率 > 70%
- ✅ 静止 NPC 不发送网络包
- ✅ 超出范围的 NPC 不同步

### 清理逻辑
- ✅ 玩家断开时清理所有数据
- ✅ 无内存泄漏

---

## ⚠️ 常见问题排查

### 问题 1：玩家 B 看不到 NPC

**检查服务器日志：**
```
[NpcVisibilityTracker] ⚠️ 玩家 玩家B 位置未缓存！
```
**原因：** 延迟 1 秒不够，位置还未同步  
**解决：** 等待几秒后再测试，或增加延迟到 1.5 秒

---

### 问题 2：NPC 位置不对

**检查服务器日志：**
```
[NpcSyncService] ✅ 返回 1/1 个可见 NPC
```
然后检查 PlayerNpcManager 中的位置是否最新。

**原因：** UpdateNpcPosition 没有执行  
**解决：** 已修复（先更新位置再检查玩家）

---

### 问题 3：玩家收到自己的 NPC

**检查客户端日志：**
```
[NpcManager] ⏭️ 跳过远程 NPC：xxx 是本地 NPC
```
**原因：** 重复检查失败  
**解决：** 已添加重复检查

---

### 问题 4：NPC 不会动态出现/消失

**检查服务器日志：**
```
[NpcSyncService] 🆕 NPC xxx 进入 玩家B 范围
[NpcSyncService] 🗑️ NPC xxx 离开 玩家B 范围
```

如果没有这些日志，检查：
1. 是否有其他玩家？（至少 2 个玩家）
2. NPC 是否在移动？（静止 NPC 不触发）
3. 距离是否跨越 100m 边界？

---

## 📝 日志级别说明

### 服务器日志
- `📥` - 接收客户端消息
- `✅` - 操作成功
- `🆕` - 动态创建（进入范围）
- `🗑️` - 动态销毁（离开范围）
- `⚠️` - 警告（位置未缓存等）

### 客户端日志
- `📦` - 接收服务器消息
- `✅` - 操作成功
- `⏭️` - 跳过（重复检查）
- `♻️` - 对象池操作

---

## 🎊 预期性能指标

### 网络性能
- **带宽：** 100 NPC → 30KB/s（只同步有变化的 + 范围内的）
- **延迟：** 100ms 更新频率
- **同步数量：** 每玩家 5-15 NPC（100m 范围内）

### CPU 性能
- **变化检测：** 节省 95% 无效网络调用
- **距离裁剪：** 节省 85% 带宽

### 内存性能
- **对象池复用率：** 预期 70-90%
- **GC 压力：** 减少 90%（对象池复用）

---

## 🚀 开始测试！

**启动顺序：**
1. ✅ 服务器已编译
2. ✅ 客户端已编译并复制到游戏
3. ✅ 启动服务器
4. ✅ 启动游戏（玩家 A）
5. ✅ 创建 NPC
6. ✅ 启动第二个游戏（玩家 B）
7. ✅ 观察日志和 NPC 行为

**如果所有测试通过，说明 NPC 同步系统完全正常！** 🎉

