namespace DuckyNet.Server.Plugin
{
    /// <summary>
    /// 插件日志接口
    /// </summary>
    public interface IPluginLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
        void Debug(string message);
    }

    /// <summary>
    /// 插件日志实现
    /// </summary>
    public class PluginLogger : IPluginLogger
    {
        private readonly string _pluginName;

        public PluginLogger(string pluginName)
        {
            _pluginName = pluginName;
        }

        public void Info(string message)
        {
            System.Console.WriteLine($"[Plugin:{_pluginName}] INFO: {message}");
        }

        public void Warning(string message)
        {
            System.Console.WriteLine($"[Plugin:{_pluginName}] WARN: {message}");
        }

        public void Error(string message)
        {
            System.Console.WriteLine($"[Plugin:{_pluginName}] ERROR: {message}");
        }

        public void Debug(string message)
        {
            System.Console.WriteLine($"[Plugin:{_pluginName}] DEBUG: {message}");
        }
    }
}

