# 武器特效播放器 (WeaponEffectsPlayer)

## 概述

封装的武器特效播放工具类，用于手动触发武器相关的视觉和音效效果。

## 功能列表

### 1. 播放枪口火焰 🔥
```csharp
WeaponEffectsPlayer.PlayMuzzleFlash();
```
- 在枪口位置实例化火焰特效预制体
- 特效自动跟随枪口移动
- 粒子系统结束后自动销毁

### 2. 播放弹壳抛出 🎆
```csharp
WeaponEffectsPlayer.PlayShellEjection();
```
- 触发弹壳粒子系统发射一个粒子
- 使用枪械配置的弹壳粒子系统

### 3. 播放开枪音效 🔊
```csharp
WeaponEffectsPlayer.PlayShootSound();
```
- 播放开枪音效（自动处理消音器）
- 音效路径：`SFX/Combat/Gun/Shoot/{shootKey}`
- 消音器路径：`SFX/Combat/Gun/Shoot/{shootKey}_mute`

**FMOD 3D 空间音效特性：**
- ✅ **音量随距离衰减** - 远处的枪声会变小
- ✅ **声音方向性** - 可以听出左右前后方向
- ✅ **实时位置更新** - 音源跟随枪械移动（如 AI 开枪）
- ✅ **监听器跟随主角** - 音频监听器位置 = 主角位置 + 向上2米
- ✅ **遮挡和混响** - FMOD 支持环境音效（如果配置）

### 4. 创建子弹 💥
```csharp
WeaponEffectsPlayer.CreateBullet();
```
- 从对象池获取子弹
- 设置子弹位置和方向
- 注意：子弹可能需要额外的初始化参数

### 5. 播放完整特效 ✨
```csharp
WeaponEffectsPlayer.PlayFullFireEffects();
```
- 一次性播放：枪口火焰 + 弹壳 + 音效
- 不包括子弹创建（避免误伤）

## 使用示例

### 在代码中调用

```csharp
using DuckyNet.Client.Core.Utils;

// 初始化（首次使用前调用一次）
WeaponEffectsPlayer.Initialize();

// 播放单个特效
WeaponEffectsPlayer.PlayMuzzleFlash();
WeaponEffectsPlayer.PlayShellEjection();
WeaponEffectsPlayer.PlayShootSound();

// 或播放完整特效
WeaponEffectsPlayer.PlayFullFireEffects();
```

### 在调试模块中使用

WeaponInfoModule 已集成特效播放功能：

1. 按 **F3** 打开调试窗口
2. 找到 **"玩家"** 分类
3. 启用 **"武器信息"** 模块
4. 点击特效测试按钮：
   - **🔥 枪口火焰** - 单独播放枪口特效
   - **🎆 弹壳抛出** - 单独播放弹壳
   - **🔊 开枪音效** - 单独播放音效
   - **✨ 完整特效** - 播放所有特效

## 实现原理

### 资源获取流程

```
1. 获取主角
   CharacterMainControl.Main
   
2. 获取当前枪械
   player.GetGun()
   
3. 获取枪械配置
   gun.GunItemSetting
   
4. 获取各种资源
   - muzzleFxPfb (枪口特效)
   - bulletPfb (子弹预制体)
   - shellParticle (弹壳粒子)
   - shootKey (音效键)
   - muzzle (枪口 Transform)
```

### 特效播放方式

#### 枪口火焰
```csharp
GameObject fx = Instantiate(muzzleFxPfb, muzzle.position, muzzle.rotation);
fx.transform.SetParent(muzzle); // 跟随枪口移动
```

#### 弹壳粒子
```csharp
shellParticle.Emit(1); // 发射一个粒子
```

#### 音效（FMOD 3D 空间音效）
```csharp
// 播放 3D 音效，附加到枪械 GameObject
AudioManager.Post(soundPath, gun.gameObject);

// 底层实现（自动执行）：
// 1. AudioObject.set3DAttributes(gun.transform.position)  - 设置音源位置
// 2. AudioObject.FixedUpdate() - 每帧更新音源位置（跟随移动）
// 3. AudioManager.UpdateListener() - 更新监听器位置（跟随主角）
```

**3D 音效工作流程：**
1. 音效附加到枪械的 GameObject
2. FMOD 使用 `set3DAttributes()` 设置音源的 3D 空间位置
3. 每帧更新音源位置（支持移动音源，如 AI 开枪）
4. 音频监听器跟随主角移动（主角位置 + 向上2米）
5. FMOD 根据音源和监听器的相对位置计算：
   - 音量衰减（距离）
   - 立体声定位（方向）
   - 环境效果（遮挡、混响）

#### 子弹（从对象池）
```csharp
Projectile bullet = LevelManager.Instance.BulletPool.GetABullet(bulletPfb);
bullet.transform.position = muzzle.position;
bullet.transform.rotation = Quaternion.LookRotation(muzzle.forward);
```

## 技术细节

### 反射缓存
所有反射操作在 `Initialize()` 中完成，避免运行时开销：
- 类型：`CharacterMainControl`, `ItemAgent_Gun`, `ItemSetting_Gun`
- 方法：`GetGun()`, `TryGetCharacter()`
- 属性：`Main`, `GunItemSetting`, `muzzle`, `Silenced`
- 字段：`shellParticle` (私有字段)

### 错误处理
- 完整的 try-catch 保护
- 友好的错误提示
- 不会影响游戏正常运行

### 性能考虑
- 反射结果缓存
- 只在需要时执行
- 不会每帧调用

## FMOD 3D 音效系统详解

### 音效类型

游戏使用 **FMOD 3D 空间音效系统**，所有通过 `AudioManager.Post(eventName, gameObject)` 播放的音效都是 3D 音效。

### 3D 音效特性

1. **距离衰减**
   - 音量随着玩家与音源的距离增加而降低
   - 符合真实物理的平方反比衰减

2. **方向性定位**
   - 左侧的枪声会从左声道播放
   - 右侧的枪声会从右声道播放
   - 可以通过声音判断枪声来源方向

3. **实时位置更新**
   - 音源附加到 GameObject（如枪械）
   - `AudioObject.FixedUpdate()` 每帧更新音源位置
   - 支持移动音源（如 AI 边跑边射击）

4. **监听器跟随主角**
   - 音频监听器位置 = `主角位置 + Vector3.up * 2`
   - 监听器旋转 = 游戏相机旋转
   - 每帧更新，确保 3D 音效准确

### 音效播放流程

```
调用 AudioManager.Post(soundPath, gunGameObject)
    ↓
创建 FMOD EventInstance
    ↓
AudioObject.set3DAttributes(gun.transform.position) ← 设置音源位置
    ↓
AudioObject.FixedUpdate() ← 每帧更新位置
    ↓
FMOD 计算：
  • 音源位置 (gun.transform.position)
  • 监听器位置 (player.position + up*2)
  • 相对距离 → 音量衰减
  • 相对方向 → 立体声定位
    ↓
输出到耳机/音箱
```

## 注意事项

1. **子弹创建谨慎使用**
   - 子弹需要额外的物理和碰撞参数
   - 可能造成误伤或意外效果
   - 仅用于测试，不建议在正式环境使用

2. **音效会叠加**
   - 快速连续点击会产生多个 3D 音效实例
   - 每个音效都有独立的空间位置
   - FMOD 会自动混音

3. **音效距离测试**
   - 可以通过走远测试音量衰减
   - 绕着枪械走动测试方向性
   - 音效附加到枪械，会跟随移动

4. **特效不会自动清理**
   - 粒子特效会在生命周期结束后自动销毁
   - 但频繁触发可能产生大量实例

5. **需要持有枪械**
   - 所有功能都需要主角当前持有枪械
   - 未持枪时会输出警告

## 测试 3D 音效的方法

1. **距离测试**
   - 点击"开枪音效"按钮
   - 控制主角向后退
   - 观察音量是否逐渐减小

2. **方向性测试**
   - 点击"开枪音效"按钮
   - 戴上耳机
   - 绕着枪械走动
   - 观察声音是否从左/右声道传来

3. **移动音源测试**
   - 找一个会移动的 AI
   - 监听 AI 的枪声
   - 观察音效是否跟随 AI 移动

## 扩展建议

### 添加后坐力模拟
```csharp
public static void PlayRecoilEffect(object gun)
{
    // 获取相机并施加后坐力
    var camera = Camera.main;
    if (camera != null)
    {
        // 实现后坐力逻辑
    }
}
```

### 添加射击相机震动
```csharp
public static void PlayCameraShake()
{
    // 触发相机震动效果
}
```

### 添加子弹轨迹线
```csharp
public static void DrawBulletTracer(Vector3 start, Vector3 end)
{
    // 绘制子弹轨迹
}
```

## 相关文件

- 工具类：`Client/Core/Utils/WeaponEffectsPlayer.cs`
- 调试模块：`Client/Core/DebugModule/Modules/WeaponInfoModule.cs`
- 游戏源码参考：`TeamSoda.Duckov.Core/ItemAgent_Gun.cs`

