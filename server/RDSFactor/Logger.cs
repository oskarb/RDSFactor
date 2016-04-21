using System;
using RADAR;

namespace RDSFactor
{
    class Logger
    {
        public static bool Debug;
        private static LogWriter _log;

        public static void Initialize(string logfilePath)
        {
            _log = new LogWriter(logfilePath);
            _log.WriteLog(
                "---------------------------------------------------------------------------------------------------");
        }


        public static void LogDebug(RADIUSPacket packet, string message)
        {
            var fromAddress = packet.EndPoint.Address.ToString();
            message = "[" + packet.UserName + " " + fromAddress + "] " + message;
            LogDebug(message);
        }


        public static void LogDebug(string message)
        {
            if (Debug)
            {
                message = DateTime.Now + ": DEBUG: " + message;

                _log?.WriteLog(message);

                // Also write to the console if not a service
                if (Environment.UserInteractive)
                    Console.WriteLine(message);
            }
        }

        public static void LogInfo(string message)
        {
            message = DateTime.Now + ": INFO: " + message;

            _log?.WriteLog(message);

            // Also write to the console if not a service
            if (Environment.UserInteractive)
                Console.WriteLine(message);
        }
    }
}