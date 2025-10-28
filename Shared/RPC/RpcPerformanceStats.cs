using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DuckyNet.Shared.RPC
{
    /// <summary>
    /// RPC 性能统计
    /// </summary>
    public class RpcPerformanceStats
    {
        private readonly Dictionary<string, ServiceStats> _serviceStats = new Dictionary<string, ServiceStats>();
        private readonly object _lock = new object();

        /// <summary>
        /// 记录方法调用
        /// </summary>
        public void RecordCall(string serviceName, string methodName, TimeSpan duration, bool success)
        {
            lock (_lock)
            {
                if (!_serviceStats.TryGetValue(serviceName, out var serviceStats))
                {
                    serviceStats = new ServiceStats(serviceName);
                    _serviceStats[serviceName] = serviceStats;
                }

                serviceStats.RecordCall(methodName, duration, success);
            }
        }

        /// <summary>
        /// 获取服务统计信息
        /// </summary>
        public ServiceStats? GetServiceStats(string serviceName)
        {
            lock (_lock)
            {
                return _serviceStats.TryGetValue(serviceName, out var stats) ? stats : null;
            }
        }

        /// <summary>
        /// 获取所有服务统计信息
        /// </summary>
        public Dictionary<string, ServiceStats> GetAllStats()
        {
            lock (_lock)
            {
                return new Dictionary<string, ServiceStats>(_serviceStats);
            }
        }

        /// <summary>
        /// 清除统计信息
        /// </summary>
        public void ClearStats()
        {
            lock (_lock)
            {
                _serviceStats.Clear();
            }
        }
    }

    /// <summary>
    /// 服务统计信息
    /// </summary>
    public class ServiceStats
    {
        public string ServiceName { get; }
        private readonly Dictionary<string, MethodStats> _methodStats = new Dictionary<string, MethodStats>();

        public ServiceStats(string serviceName)
        {
            ServiceName = serviceName;
        }

        public void RecordCall(string methodName, TimeSpan duration, bool success)
        {
            if (!_methodStats.TryGetValue(methodName, out var methodStats))
            {
                methodStats = new MethodStats(methodName);
                _methodStats[methodName] = methodStats;
            }

            methodStats.RecordCall(duration, success);
        }

        public MethodStats? GetMethodStats(string methodName)
        {
            return _methodStats.TryGetValue(methodName, out var stats) ? stats : null;
        }

        public Dictionary<string, MethodStats> GetAllMethodStats()
        {
            return new Dictionary<string, MethodStats>(_methodStats);
        }
    }

    /// <summary>
    /// 方法统计信息
    /// </summary>
    public class MethodStats
    {
        public string MethodName { get; }
        public long TotalCalls { get; private set; }
        public long SuccessfulCalls { get; private set; }
        public long FailedCalls { get; private set; }
        public TimeSpan TotalDuration { get; private set; }
        public TimeSpan MinDuration { get; private set; } = TimeSpan.MaxValue;
        public TimeSpan MaxDuration { get; private set; } = TimeSpan.MinValue;

        public MethodStats(string methodName)
        {
            MethodName = methodName;
        }

        public void RecordCall(TimeSpan duration, bool success)
        {
            TotalCalls++;
            TotalDuration = TotalDuration.Add(duration);

            if (duration < MinDuration)
                MinDuration = duration;
            if (duration > MaxDuration)
                MaxDuration = duration;

            if (success)
                SuccessfulCalls++;
            else
                FailedCalls++;
        }

        public TimeSpan AverageDuration => TotalCalls > 0 ? 
            TimeSpan.FromTicks(TotalDuration.Ticks / TotalCalls) : TimeSpan.Zero;

        public double SuccessRate => TotalCalls > 0 ? 
            (double)SuccessfulCalls / TotalCalls : 0.0;
    }

    /// <summary>
    /// 性能监控辅助类
    /// </summary>
    public class RpcPerformanceMonitor : IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private readonly RpcPerformanceStats _stats;
        private readonly string _serviceName;
        private readonly string _methodName;

        public RpcPerformanceMonitor(RpcPerformanceStats stats, string serviceName, string methodName)
        {
            _stats = stats;
            _serviceName = serviceName;
            _methodName = methodName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _stats.RecordCall(_serviceName, _methodName, _stopwatch.Elapsed, true);
        }

        public void RecordFailure()
        {
            _stopwatch.Stop();
            _stats.RecordCall(_serviceName, _methodName, _stopwatch.Elapsed, false);
        }
    }
}