using System;
using RADAR;

namespace RDSFactor
{
    class Logger
    {
        public static bool DEBUG;
        public static LogWriter Log = new LogWriter();

        public static void LogDebug(RADIUSPacket packet, string message)
        {
            var from_address = packet.EndPoint.Address.ToString();
            message = "[" + packet.UserName + " " + from_address + "] " + message;
            LogDebug(message);
        }

        public static void LogDebug(string message)
        {
            message = DateTime.Now + ": DEBUG: " + message;
            if (DEBUG)
            {
                Log.WriteLog(message);

                // Also write to the console if not a service
                if (Environment.UserInteractive)
                    Console.WriteLine(message);
            }
        }

        public static void LogInfo(string message)
        {
            message = DateTime.Now + ": INFO: " + message;
            Log.WriteLog(message);
            // Also write to the console if not a service
            if (Environment.UserInteractive)
                Console.WriteLine(message);
        }
    }
}