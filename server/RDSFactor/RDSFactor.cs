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
            Logger.Initialize(ApplicationPath() + "\\log.txt",
                ApplicationPath() + @"\conf\log4net.config.xml");

            Logger.LogInfo("Starting Service.");
            Logger.LogDebug("Loading Configuration...");
            LoadConfiguration();
            StartUpServer();
        }

        protected override void OnStop()
        {
            Logger.LogInfo("Stopping Radius listener ports.");
        }


        public void StartUpServer()
        {
            try
            {
                server = new RADIUSServer(serverPort, ProcessPacket, ref Config.Secrets);
                Logger.LogInfo($"Starting Radius Server on port {serverPort} successful.");
            }
            catch (Exception)
            {
                Logger.LogError($"Starting Radius Server on Port {serverPort} failed.");
            }
        }


        private void ProcessPacket(RADIUSPacket packet)
        {
            if (!packet.IsValid)
            {
                Logger.LogError("Packet is not valid. Discarding.");
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


        private void LoadConfiguration()
        {
            bool confOk = true;
            IniFile rConfig = new IniFile();

            try
            {
                rConfig.Load(ApplicationPath() + @"\conf\RDSFactor.ini");
                Logger.Debug = rConfig.GetKeyValue<bool>("RDSFactor", "Debug");

                Config.LDAPDomain = rConfig.GetKeyValue("RDSFactor", "LDAPDomain");
                if (Config.LDAPDomain.Length == 0)
                {
                    Logger.LogError("LDAPDomain can not be empty.");
                    confOk = false;
                }

                TSGW = rConfig.GetKeyValue("RDSFactor", "TSGW");

                Config.EnableOTP = rConfig.GetKeyValue<bool>("RDSFactor", "EnableOTP");

                if (Config.EnableOTP)
                {
                    if (rConfig.GetKeyValue<bool>("RDSFactor", "EnableEmail"))
                    {
                        EnableEmail = true;
                        Sender.SenderEmail = rConfig.GetKeyValue("RDSFactor", "SenderEmail");
                        Sender.MailServer = rConfig.GetKeyValue("RDSFactor", "MailServer");
                        Config.ADMailAttribute = rConfig.GetKeyValue("RDSFactor", "ADMailAttribute");
                    }

                    var adPhoneAttribute = rConfig.GetKeyValue("RDSFactor", "ADPhoneAttributes");
                    if (!string.IsNullOrWhiteSpace(adPhoneAttribute))
                    {
                        // Configured value can be any of:
                        //      someAttribute
                        //      someAttribute,otherAttribute  (arbitrary length)
                        // When parsing, also be forgiving of trailing or extra commas, and whitespace around commas.

                        Config.ADPhoneAttributes =
                            adPhoneAttribute.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(part => part.Trim())
                                .Where(part => !string.IsNullOrWhiteSpace(part))
                                .ToList();
                    }

                    if (Config.ADPhoneAttributes.Count == 0)
                    {
                        Logger.LogError("ADPhoneAttribute can not be empty. Specify" +
                                       " the name of one or multiple LDAP attributes, separated by comma." +
                                       " The value of the first non-empty attribute will be used.");
                        confOk = false;
                    }


                    if (rConfig.GetKeyValue<bool>("RDSFactor", "EnableSMS"))
                    {
                        EnableSMS = true;
                        Sender.ModemType =
                            (ModemType) rConfig.GetKeyValue<int>("RDSFactor", "USELOCALMODEM");
                        switch (Sender.ModemType)
                        {
                            case ModemType.Internet:
                                Sender.Provider = rConfig.GetKeyValue("RDSFactor", "Provider");
                                if (Sender.Provider.Length == 0)
                                {
                                    Logger.LogError("Provider can not be empty.");
                                    confOk = false;
                                }
                                break;
                            case ModemType.SmsModem:
                                Sender.ComPort = rConfig.GetKeyValue("RDSFactor", "COMPORT");
                                if (Sender.ComPort.Length == 0)
                                {
                                    Logger.LogError("ComPort can not be empty.");
                                    confOk = false;
                                }
                                Sender.SmsC = rConfig.GetKeyValue("RDSFactor", "SMSC");
                                if (Sender.SmsC.Length == 0)
                                {
                                    Logger.LogError(
                                        "SmsC can not be empty. See http://smsclist.com/downloads/default.txt for valid values.");
                                    confOk = false;
                                }
                                break;
                            default:
                                Logger.LogError("USELOCALMODEM contain invalid configuration. Correct value is 1 or 0.");
                                confOk = false;
                                break;
                        }
                    }
                }

                foreach (var client in rConfig.GetSection("clients").Keys)
                {
                    var address = client.Name;
                    Logger.LogInfo($"Adding shared secret for: {address}");
                    Config.Secrets.AddSharedSecret(address, client.Value);
                }

                if (confOk)
                    Logger.LogInfo("Loaded configuration successfully.");
                else
                    Logger.LogError("Loading configuration failed.");
            }
            catch (Exception ex)
            {
                Logger.LogError("ERROR loading configuration: " + ex);
                Logger.LogError("ERROR: Missing RDSFactor.ini from startup path or RDSFactor.ini contains invalid configuration.");
                Logger.LogError("Loading configuration failed.");
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