using System;
using System.IO;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using RADAR;

namespace RDSFactor
{
    class Logger
    {
        private static readonly ILog Log = LogManager.GetLogger("RDSFactor.Radius");


        private static bool _debug;
        private static AppenderSkeleton _appender;

        public static bool Debug
        {
            get { return _debug; }
            set
            {
                _debug = value;
                if (_appender != null)
                    _appender.Threshold = _debug ? Level.Debug : Level.Info;
            }
        }

        public static bool DebugDumpState { get; set; }

        public static void Initialize(string logfilePath, string optionalLog4NetConfigPath)
        {
            // When interactice, also activate logging to the console.
            if (Environment.UserInteractive)
                BasicConfigurator.Configure();

            // Use a full-featured log4net log4net configuration file it it exist, otherwise
            // use a simple hardcoded default.
            if (string.IsNullOrWhiteSpace(optionalLog4NetConfigPath) || !File.Exists(optionalLog4NetConfigPath))
            {
                _appender = new RollingFileAppender
                {
                    Name = "DefaultAppended",
                    File = logfilePath,
                    RollingStyle = RollingFileAppender.RollingMode.Size,
                    MaxSizeRollBackups = 10,
                    PreserveLogFileNameExtension = true,
                    Threshold = _debug ? Level.Debug : Level.Info,
                    Layout = new PatternLayout("%date [%2thread] %-5level %logger %ndc - %message%newline"),
                };
                _appender.ActivateOptions();
                BasicConfigurator.Configure(_appender);
            }
            else
            {
                XmlConfigurator.ConfigureAndWatch(new FileInfo(optionalLog4NetConfigPath));
            }

            Log.Info(new string('-', 30));
        }


        public static void LogDebug(RADIUSPacket packet, string message)
        {
            var fromAddress = packet.EndPoint.Address.ToString();
            message = "[" + packet.UserName + " " + fromAddress + "] " + message;
            LogDebug(message);
        }


        public static void LogDebug(string message)
        {
            Log.Debug(message);
        }

        public static void LogInfo(string message)
        {
            Log.Info(message);
        }

        public static void LogError(string message)
        {
            Log.Error(message);
        }

        public static void LogError(string message, Exception exception)
        {
            Log.Error(message, exception);
        }
    }
}