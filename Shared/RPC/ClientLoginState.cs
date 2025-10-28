using System;

namespace DuckyNet.Shared.RPC
{
    /// <summary>
    /// 客户端登录状态
    /// </summary>
    public enum ClientLoginState
    {
        /// <summary>
        /// 未登录（刚连接，等待登录）
        /// </summary>
        NotLoggedIn,

        /// <summary>
        /// 登录中（已发送登录请求，等待响应）
        /// </summary>
        LoggingIn,

        /// <summary>
        /// 已登录（登录成功，可以使用所有功能）
        /// </summary>
        LoggedIn,

        /// <summary>
        /// 登录失败
        /// </summary>
        LoginFailed
    }

    /// <summary>
    /// 登录状态管理器
    /// </summary>
    public class LoginStateManager
    {
        private ClientLoginState _state = ClientLoginState.NotLoggedIn;
        private readonly object _lock = new object();
        private DateTime _connectionTime;
        private DateTime? _loginRequestTime;

        public ClientLoginState State
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
        }

        /// <summary>
        /// 是否已登录（可以发送其他RPC请求）
        /// </summary>
        public bool IsLoggedIn => State == ClientLoginState.LoggedIn;

        /// <summary>
        /// 是否正在登录
        /// </summary>
        public bool IsLoggingIn => State == ClientLoginState.LoggingIn;

        /// <summary>
        /// 是否需要登录（连接后尚未登录）
        /// </summary>
        public bool NeedsLogin => State == ClientLoginState.NotLoggedIn;

        /// <summary>
        /// 连接后经过的时间（秒）
        /// </summary>
        public double SecondsSinceConnection
        {
            get
            {
                lock (_lock)
                {
                    return (DateTime.UtcNow - _connectionTime).TotalSeconds;
                }
            }
        }

        /// <summary>
        /// 登录状态变化事件
        /// </summary>
        public event EventHandler<ClientLoginState>? StateChanged;

        /// <summary>
        /// 当连接建立时调用
        /// </summary>
        public void OnConnected()
        {
            lock (_lock)
            {
                _state = ClientLoginState.NotLoggedIn;
                _connectionTime = DateTime.UtcNow;
                _loginRequestTime = null;
            }
            RpcLog.Info("[LoginState] Connected, waiting for login");
        }

        /// <summary>
        /// 当开始登录时调用
        /// </summary>
        public void OnLoginStarted()
        {
            lock (_lock)
            {
                if (_state != ClientLoginState.NotLoggedIn)
                {
                    RpcLog.Warning($"[LoginState] Login started in wrong state: {_state}");
                }
                _state = ClientLoginState.LoggingIn;
                _loginRequestTime = DateTime.UtcNow;
            }
            RpcLog.Info("[LoginState] Login request sent");
            StateChanged?.Invoke(this, ClientLoginState.LoggingIn);
        }

        /// <summary>
        /// 当登录成功时调用
        /// </summary>
        public void OnLoginSucceeded()
        {
            lock (_lock)
            {
                _state = ClientLoginState.LoggedIn;
                if (_loginRequestTime.HasValue)
                {
                    var duration = (DateTime.UtcNow - _loginRequestTime.Value).TotalMilliseconds;
                    RpcLog.Info($"[LoginState] Login succeeded in {duration:F0}ms");
                }
            }
            StateChanged?.Invoke(this, ClientLoginState.LoggedIn);
        }

        /// <summary>
        /// 当登录失败时调用
        /// </summary>
        public void OnLoginFailed(string reason)
        {
            lock (_lock)
            {
                _state = ClientLoginState.LoginFailed;
            }
            RpcLog.Error($"[LoginState] Login failed: {reason}");
            StateChanged?.Invoke(this, ClientLoginState.LoginFailed);
        }

        /// <summary>
        /// 当断开连接时调用
        /// </summary>
        public void OnDisconnected()
        {
            lock (_lock)
            {
                _state = ClientLoginState.NotLoggedIn;
            }
        }

        /// <summary>
        /// 检查是否可以发送RPC请求
        /// </summary>
        public bool CanSendRpc()
        {
            return IsLoggedIn;
        }

        /// <summary>
        /// 如果未登录则抛出异常
        /// </summary>
        public void EnsureLoggedIn()
        {
            if (!IsLoggedIn)
            {
                throw new InvalidOperationException(
                    $"Cannot send RPC request: not logged in (current state: {State})");
            }
        }
    }
}

