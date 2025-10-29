using System;

namespace DuckyNet.Shared.RPC
{
    /// <summary>
    /// RPC连接状态枚举
    /// </summary>
    public enum RpcConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Disconnecting
    }

    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public RpcConnectionState OldState { get; }
        public RpcConnectionState NewState { get; }
        public string? Reason { get; }

        public ConnectionStateChangedEventArgs(RpcConnectionState oldState, RpcConnectionState newState, string? reason = null)
        {
            OldState = oldState;
            NewState = newState;
            Reason = reason;
        }
    }

    /// <summary>
    /// 连接管理器
    /// </summary>
    public class ConnectionManager
    {
        private RpcConnectionState _state = RpcConnectionState.Disconnected;
        private readonly object _lock = new object();

        public RpcConnectionState State
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
            private set
            {
                lock (_lock)
                {
                    if (_state != value)
                    {
                        var oldState = _state;
                        _state = value;
                        OnStateChanged(oldState, value);
                    }
                }
            }
        }

        public bool IsConnected => State == RpcConnectionState.Connected;
        public bool CanConnect => State == RpcConnectionState.Disconnected;

        public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

        public void SetState(RpcConnectionState newState, string? reason = null)
        {
            var oldState = State;
            State = newState;
            
            if (oldState != newState)
            {
                Console.WriteLine($"Connection state changed: {oldState} -> {newState}" + 
                    (reason != null ? $" ({reason})" : ""));
            }
        }

        private void OnStateChanged(RpcConnectionState oldState, RpcConnectionState newState)
        {
            StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState));
        }
    }
}

