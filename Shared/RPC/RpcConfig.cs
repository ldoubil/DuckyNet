namespace DuckyNet.Shared.RPC
{
    /// <summary>
    /// RPC 配置
    /// </summary>
    public class RpcConfig
    {
        /// <summary>
        /// 默认RPC调用超时时间（毫秒）
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 心跳间隔（毫秒）
        /// </summary>
        public int HeartbeatIntervalMs { get; set; } = 5000;

        /// <summary>
        /// 最大重连次数
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 5;

        /// <summary>
        /// 重连延迟（毫秒）
        /// </summary>
        public int ReconnectDelayMs { get; set; } = 2000;

        /// <summary>
        /// 最大并发RPC调用数
        /// </summary>
        public int MaxConcurrentCalls { get; set; } = 100;

        /// <summary>
        /// 是否启用详细日志
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>
        /// 最大客户端连接数（服务器）
        /// </summary>
        public int MaxClients { get; set; } = 100;

        /// <summary>
        /// 默认配置
        /// </summary>
        public static RpcConfig Default => new RpcConfig();

        /// <summary>
        /// 开发模式配置（更多日志）
        /// </summary>
        public static RpcConfig Development => new RpcConfig
        {
            EnableVerboseLogging = true,
            DefaultTimeoutMs = 60000
        };

        /// <summary>
        /// 生产模式配置（优化性能）
        /// </summary>
        public static RpcConfig Production => new RpcConfig
        {
            EnableVerboseLogging = false,
            DefaultTimeoutMs = 15000
        };
    }
}

