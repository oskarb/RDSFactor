using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using RADAR;
using RDSFactor.Handlers;

namespace RDSFactor
{
    partial class RDSFactor : ServiceBase
    {
        public static string LDAPDomain = "";
        public static string ADMobileField = "";
        public static string ADMailField = "";
        public static bool EnableOTP;
        public static NASAuthList secrets = new NASAuthList();


        public static int SessionTimeOut = 30; // in minutes
        public static int LaunchTimeOut = 30; // in seconds
        public static int garbageCollectionInterval = 60*60*1000; //' in millis
        public static bool EnableSMS = false;
        public static bool EnableEmail = false;

        private RADIUSServer server;
        private int serverPort = 1812;
        private Dictionary<string, string> userHash = new Dictionary<string, string>();
        private Dictionary<string, string> packetHash = new Dictionary<string, string>();
        private Dictionary<string, string> clientHash = new Dictionary<string, string>();

        private string TSGW = "";


        public RDSFactor()
        {
            InitializeComponent();

            cleanupEvent.Interval = garbageCollectionInterval;
        }


        public void StartInteractive(string[] args)
        {
            OnStart(args);
        }


        public void StopInteractive()
        {
            OnStop();
        }


        protected override void OnStart(string[] args)
        {
            Logger.Log.filePath = ApplicationPath() + "\\log.txt";
            Logger.Log.WriteLog(
                "---------------------------------------------------------------------------------------------------");
            Logger.LogInfo("Starting Service");
            Logger.LogInfo("Loading Configuration...");
            LoadConfiguration();
            Logger.LogInfo("Starting Radius listner ports...");
            StartUpServer();
        }

        protected override void OnStop()
        {
            Logger.LogInfo("Stopping Radius listner ports...");
        }


        public void StartUpServer()
        {
            try
            {
                server = new RADIUSServer(serverPort, ProcessPacket, ref secrets);
                Logger.LogInfo("Starting Radius Server on Port " + serverPort + " ...OK");
            }
            catch (Exception)
            {
                Logger.LogInfo("Starting Radius Server on Port " + serverPort + "...FAILED");
            }
        }


        private void ProcessPacket(RADIUSPacket packet)
        {
            if (!packet.IsValid)
            {
                Console.WriteLine("Packet is not valid. Discarding.");
                return;
            }

            var handler = new RDSHandler(packet);

            // If TSGW = "1" Then
            //   handler = New RDSHandler(packet)
            // Else
            //   handler = New CitrixHandler(packet)
            // End If

            handler.ProcessRequest();
        }


        public static string GenerateCode()
        {
            Random ordRand = new System.Random();
            int[] temp = new int[6];

            for (int i = 0; i < temp.Length; i++)
            {
                var dummy = ordRand.Next(1, 9);
                if (!temp.Contains(dummy))
                {
                    temp[i] = dummy;
                }
            }

            var code = string.Join(string.Empty, temp.Select(i => i.ToString()));
            return code;
        }


        private void LoadConfiguration()
        {
            bool confOk = true;
            IniFile rConfig = new IniFile();

            try
            {
                rConfig.Load(ApplicationPath() + @"\conf\RDSFactor.ini");
                Logger.DEBUG = Convert.ToBoolean(rConfig.GetKeyValue("RDSFactor", "Debug"));

                LDAPDomain = rConfig.GetKeyValue("RDSFactor", "LDAPDomain");
                if (LDAPDomain.Length == 0)
                {
                    Logger.LogInfo("ERROR: LDAPDomain can not be empty");
                    confOk = false;
                }

                TSGW = rConfig.GetKeyValue("RDSFactor", "TSGW");

                EnableOTP = Convert.ToBoolean(rConfig.GetKeyValue("RDSFactor", "EnableOTP"));

                if (EnableOTP)
                {
                    if (rConfig.GetKeyValue("RDSFactor", "EnableEmail") == "1")
                    {
                        EnableEmail = true;
                        Sender.SenderEmail = rConfig.GetKeyValue("RDSFactor", "SenderEmail");
                        Sender.MailServer = rConfig.GetKeyValue("RDSFactor", "MailServer");
                        ADMailField = rConfig.GetKeyValue("RDSFactor", "ADMailField");
                    }

                    ADMobileField = rConfig.GetKeyValue("RDSFactor", "ADField");
                    if (ADMobileField.Length == 0)
                    {
                        Logger.LogInfo("ERROR:  ADField can not be empty");
                        confOk = false;
                    }

                    if (rConfig.GetKeyValue("RDSFactor", "EnableSMS") == "1")
                    {
                        EnableSMS = true;
                        Sender.ModemType = Convert.ToInt32(rConfig.GetKeyValue("RDSFactor", "USELOCALMODEM"));
                        switch (Sender.ModemType)
                        {
                            case 0:
                                Sender.Provider = rConfig.GetKeyValue("RDSFactor", "Provider");
                                if (Sender.Provider.Length == 0)
                                {
                                    Logger.LogInfo("ERROR:  Provider can not be empty");
                                    confOk = false;
                                }
                                break;
                            case 1:
                                Sender.ComPort = rConfig.GetKeyValue("RDSFactor", "COMPORT");
                                if (Sender.ComPort.Length == 0)
                                {
                                    Logger.LogInfo("ERROR:  ComPort can not be empty");
                                    confOk = false;
                                }
                                Sender.SmsC = rConfig.GetKeyValue("RDSFactor", "SMSC");
                                if (Sender.SmsC.Length == 0)
                                {
                                    Logger.LogInfo(
                                        "ERROR:  SmsC can not be empty. See http://smsclist.com/downloads/default.txt for valid values");
                                    confOk = false;
                                }
                                break;
                            default:
                                Logger.LogInfo("ERROR:  USELOCALMODEM contain invalid configuration. Correct value are 1 or 0");
                                confOk = false;
                                break;
                        }
                    }
                }

                foreach (var client in rConfig.GetSection("clients").Keys)
                {
                    var address = client.Name;
                    Logger.LogInfo("Adding Shared Secret for: " + address);
                    secrets.AddSharedSecret(address, client.Value);
                }

                if (confOk)
                    Logger.LogInfo("Loading Configuration...OK");
                else
                    Logger.LogInfo("Loading Configuration...FAILED");
            }
            catch (Exception)
            {
                Logger.LogInfo("ERROR: Missing RDSFactor.ini from startup path or RDSFactor.ini contains invalid configuration");
                Logger.LogInfo("Loading Configuration...FAILED");
                Environment.Exit(1);
            }
        }


        public string ApplicationPath()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }


        public void cleanupEvent_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            RDSHandler.Cleanup();
        }
    }
}