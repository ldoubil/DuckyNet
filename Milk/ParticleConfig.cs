using UnityEngine;

namespace Milk
{
    /// <summary>
    /// 牛奶粒子系统配置类
    /// 包含所有粒子系统相关的常量配置
    /// </summary>
    public static class ParticleConfig
    {
        #region 基础粒子配置

        /// <summary>
        /// 粒子初始发射速度 (m/s)
        /// </summary>
        public const float SHOOT_FORCE = 20f;

        /// <summary>
        /// 重力倍数（1.0 = 正常重力）
        /// </summary>
        public const float GRAVITY_MODIFIER = 1.0f;

        /// <summary>
        /// 每次发射的粒子数量
        /// </summary>
        public const int PARTICLE_COUNT = 150;

        /// <summary>
        /// 发射位置高度偏移（米）- 从角色脚底向上的高度
        /// </summary>
        public const float SHOOT_HEIGHT_OFFSET = 1f;

        /// <summary>
        /// 发射位置前方偏移（米）- 避免被玩家自己挡住
        /// </summary>
        public const float SHOOT_FORWARD_OFFSET = 0.8f;

        /// <summary>
        /// 粒子持续时间 - 最小值（秒）
        /// </summary>
        public const float PARTICLE_LIFETIME_MIN = 1.0f;

        /// <summary>
        /// 粒子持续时间 - 最大值（秒）
        /// </summary>
        public const float PARTICLE_LIFETIME_MAX = 3.0f;

        /// <summary>
        /// 粒子大小 - 最小值（米）
        /// </summary>
        public const float PARTICLE_SIZE_MIN = 0.15f;

        /// <summary>
        /// 粒子大小 - 最大值（米）
        /// </summary>
        public const float PARTICLE_SIZE_MAX = 0.35f;

        /// <summary>
        /// 粒子系统自动销毁时间（秒）
        /// </summary>
        public const float PARTICLE_SYSTEM_LIFETIME = 10f;

        /// <summary>
        /// 粒子发射锥形角度（度）
        /// </summary>
        public const float EMISSION_CONE_ANGLE = 15f;

        /// <summary>
        /// 粒子发射锥形半径（米）
        /// </summary>
        public const float EMISSION_CONE_RADIUS = 0.1f;

        /// <summary>
        /// 粒子发射位置分布（0=中心，1=边缘）
        /// </summary>
        public const float EMISSION_RADIUS_THICKNESS = 0.5f;

        /// <summary>
        /// 粒子旋转速度 - 最小值（度/秒）
        /// </summary>
        public const float ROTATION_SPEED_MIN = -180f;

        /// <summary>
        /// 粒子旋转速度 - 最大值（度/秒）
        /// </summary>
        public const float ROTATION_SPEED_MAX = 180f;

        /// <summary>
        /// 粒子爆发持续时间（秒）- 短时间爆发
        /// </summary>
        public const float BURST_DURATION = 0.2f;

        #endregion

        #region 碰撞配置

        /// <summary>
        /// 碰撞阻尼系数（0-1）
        /// </summary>
        public const float COLLISION_DAMPEN = 0.5f;

        /// <summary>
        /// 碰撞反弹系数（0-1）
        /// </summary>
        public const float COLLISION_BOUNCE = 0.3f;

        /// <summary>
        /// 碰撞后生命损失系数（0-1）
        /// </summary>
        public const float COLLISION_LIFETIME_LOSS = 0.1f;

        #endregion

        #region 拖尾配置

        /// <summary>
        /// 拖尾持续时间（秒）
        /// </summary>
        public const float TRAIL_LIFETIME = 0.3f;

        /// <summary>
        /// 拖尾顶点最小距离（米）
        /// </summary>
        public const float TRAIL_MIN_VERTEX_DISTANCE = 0.02f;

        #endregion

        #region 黏糊糊效果配置

        /// <summary>
        /// 最大线条数量（增加到支持更多拉丝）
        /// </summary>
        public const int MAX_STICKY_LINES = 200;

        /// <summary>
        /// 创建连线的最小距离（米）
        /// </summary>
        public const float MIN_CONNECTION_DISTANCE = 0.1f;

        /// <summary>
        /// 创建连线的最大距离（米）
        /// </summary>
        public const float MAX_CONNECTION_DISTANCE = 10f;

        /// <summary>
        /// 创建连线的概率（0-1）- 每次碰撞时尝试连接的概率
        /// </summary>
        public const float LINE_CREATE_CHANCE = 0.9f;

        /// <summary>
        /// 自身粘连概率（0-1）
        /// </summary>
        public const float SELF_STICK_CHANCE = 0.9f;

        /// <summary>
        /// 垂直向上法线阈值 - 超过此值视为地面
        /// </summary>
        public const float VERTICAL_NORMAL_THRESHOLD = 0.8f;

        /// <summary>
        /// 垂直向下法线阈值 - 低于此值视为天花板
        /// </summary>
        public const float CEILING_NORMAL_THRESHOLD = -0.5f;

        /// <summary>
        /// 高度差阈值（米）- 超过此值认为有高度差，可以创建连线
        /// </summary>
        public const float HEIGHT_DIFFERENCE_THRESHOLD = 0.3f;

        /// <summary>
        /// 自身粘连最大距离（米）
        /// </summary>
        public const float SELF_STICK_MAX_DISTANCE = 30f;

        /// <summary>
        /// 最大碰撞点缓存数量
        /// </summary>
        public const int MAX_COLLISION_POINTS = 100;

        /// <summary>
        /// 每次碰撞连接到最近点的数量（每个新点连接多个旧点）
        /// </summary>
        public const int NEARBY_CONNECTIONS = 5;

        /// <summary>
        /// 自身粘连连接数量（同一物体上的连接）
        /// </summary>
        public const int SELF_STICK_CONNECTIONS = 3;

        /// <summary>
        /// 是否启用连线功能（暂时禁用）
        /// </summary>
        public const bool ENABLE_STICKY_LINES = false;

        /// <summary>
        /// 黏糊糊线条存活时间（秒）
        /// </summary>
        public const float STICKY_LINE_LIFETIME = 8f;

        /// <summary>
        /// 线条拉伸断裂距离倍数 - 当拉伸超过原长度此倍数时断裂
        /// </summary>
        public const float LINE_BREAK_DISTANCE_MULTIPLIER = 3f;

        /// <summary>
        /// 线条拉伸变细速度（每秒）
        /// </summary>
        public const float LINE_THINNING_SPEED = 0.05f;

        #endregion

        #region 线条渲染配置

        /// <summary>
        /// 线条起始宽度
        /// </summary>
        public const float LINE_START_WIDTH = 0.12f;

        /// <summary>
        /// 线条结束宽度
        /// </summary>
        public const float LINE_END_WIDTH = 0.06f;

        /// <summary>
        /// 线条分段数
        /// </summary>
        public const int LINE_SEGMENTS = 8;

        /// <summary>
        /// 下垂系数（距离的倍数）
        /// </summary>
        public const float SAG_FACTOR = 0.3f;

        /// <summary>
        /// 摆动范围
        /// </summary>
        public const float WOBBLE_RANGE = 0.03f;

        /// <summary>
        /// 下垂速度（米/秒）
        /// </summary>
        public const float DRIPPING_SPEED = 0.5f;

        #endregion

        #region 贴花配置

        /// <summary>
        /// 圆形分段数
        /// </summary>
        public const int DECAL_SEGMENTS = 16;

        /// <summary>
        /// 贴花表面偏移（避免 Z-fighting）
        /// </summary>
        public const float DECAL_OFFSET = 0.01f;

        /// <summary>
        /// 贴花最小半径
        /// </summary>
        public const float DECAL_RADIUS_MIN = 0.2f;

        /// <summary>
        /// 贴花最大半径
        /// </summary>
        public const float DECAL_RADIUS_MAX = 0.4f;

        /// <summary>
        /// 边缘不规则最小值
        /// </summary>
        public const float DECAL_IRREGULARITY_MIN = 0.8f;

        /// <summary>
        /// 边缘不规则最大值
        /// </summary>
        public const float DECAL_IRREGULARITY_MAX = 1.2f;

        /// <summary>
        /// 贴花透明度
        /// </summary>
        public const float DECAL_ALPHA = 0.8f;

        /// <summary>
        /// 贴花存活时间（秒）
        /// </summary>
        public const float DECAL_LIFETIME = 6f;

        /// <summary>
        /// 贴花扩散速度（米/秒）
        /// </summary>
        public const float DECAL_SPREAD_SPEED = 0.05f;

        /// <summary>
        /// 贴花最大扩散倍数
        /// </summary>
        public const float DECAL_MAX_SPREAD_MULTIPLIER = 1.5f;

        #endregion
    }
}