using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Reflection;
using System.ServiceProcess;
using LogFile;
using RADAR;
using RDSFactor.handlers;

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

        private static bool DEBUG;
        private static LogWriter Log = new LogWriter();

        private RADIUSServer server;
        private int serverPort = 1812;
        private Dictionary<string, string> userHash = new Dictionary<string, string>();
        private Dictionary<string, string> packetHash = new Dictionary<string, string>();
        private Dictionary<string, string> clientHash = new Dictionary<string, string>();

        private static string Provider = "";
        private static int ModemType = 0;
        private static string ComPort = "";
        private static string SmsC = "";
        private static string MailServer = "";
        private static string SenderEmail = "";
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
            Log.filePath = ApplicationPath() + "\\log.txt";
            Log.WriteLog(
                "---------------------------------------------------------------------------------------------------");
            LogInfo("Starting Service");
            LogInfo("Loading Configuration...");
            loadConfiguration();
            LogInfo("Starting Radius listner ports...");
            StartUpServer();
        }

        protected override void OnStop()
        {
            LogInfo("Stopping Radius listner ports...");
        }


        public void StartUpServer()
        {
            try
            {
                server = new RADIUSServer(serverPort, ProcessPacket, ref secrets);
                LogInfo("Starting Radius Server on Port " + serverPort + " ...OK");
            }
            catch (Exception)
            {
                LogInfo("Starting Radius Server on Port " + serverPort + "...FAILED");
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


        public void loadConfiguration()
        {
            bool ConfOk = true;
            IniFile RConfig = new IniFile();

            try
            {
                RConfig.Load(ApplicationPath() + @"\conf\RDSFactor.ini");
                DEBUG = Convert.ToBoolean(RConfig.GetKeyValue("RDSFactor", "Debug"));

                LDAPDomain = RConfig.GetKeyValue("RDSFactor", "LDAPDomain");
                if (LDAPDomain.Length == 0)
                {
                    LogInfo("ERROR: LDAPDomain can not be empty");
                    ConfOk = false;
                }

                TSGW = RConfig.GetKeyValue("RDSFactor", "TSGW");

                EnableOTP = Convert.ToBoolean(RConfig.GetKeyValue("RDSFactor", "EnableOTP"));

                if (EnableOTP)
                {
                    if (RConfig.GetKeyValue("RDSFactor", "EnableEmail") == "1")
                    {
                        EnableEmail = true;
                        SenderEmail = RConfig.GetKeyValue("RDSFactor", "SenderEmail");
                        MailServer = RConfig.GetKeyValue("RDSFactor", "MailServer");
                        ADMailField = RConfig.GetKeyValue("RDSFactor", "ADMailField");
                    }

                    ADMobileField = RConfig.GetKeyValue("RDSFactor", "ADField");
                    if (ADMobileField.Length == 0)
                    {
                        LogInfo("ERROR:  ADField can not be empty");
                        ConfOk = false;
                    }

                    if (RConfig.GetKeyValue("RDSFactor", "EnableSMS") == "1")
                    {
                        EnableSMS = true;
                        ModemType = Convert.ToInt32(RConfig.GetKeyValue("RDSFactor", "USELOCALMODEM"));
                        switch (ModemType)
                        {
                            case 0:
                                Provider = RConfig.GetKeyValue("RDSFactor", "Provider");
                                if (Provider.Length == 0)
                                {
                                    LogInfo("ERROR:  Provider can not be empty");
                                    ConfOk = false;
                                }
                                break;
                            case 1:
                                ComPort = RConfig.GetKeyValue("RDSFactor", "COMPORT");
                                if (ComPort.Length == 0)
                                {
                                    LogInfo("ERROR:  ComPort can not be empty");
                                    ConfOk = false;
                                }
                                SmsC = RConfig.GetKeyValue("RDSFactor", "SMSC");
                                if (SmsC.Length == 0)
                                {
                                    LogInfo(
                                        "ERROR:  SmsC can not be empty. See http://smsclist.com/downloads/default.txt for valid values");
                                    ConfOk = false;
                                }
                                break;
                            default:
                                LogInfo("ERROR:  USELOCALMODEM contain invalid configuration. Correct value are 1 or 0");
                                ConfOk = false;
                                break;
                        }
                    }
                }

                foreach (var client in RConfig.GetSection("clients").Keys)
                {
                    var address = client.Name;
                    LogInfo("Adding Shared Secret for: " + address);
                    secrets.AddSharedSecret(address, client.Value);
                }

                if (ConfOk)
                    LogInfo("Loading Configuration...OK");
                else
                    LogInfo("Loading Configuration...FAILED");
            }
            catch (Exception)
            {
                LogInfo("ERROR: Missing RDSFactor.ini from startup path or RDSFactor.ini contains invalid configuration");
                LogInfo("Loading Configuration...FAILED");
                Environment.Exit(1);
            }
        }


        public string ApplicationPath()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }


        public static void SendSMS(string number, string passcode)
        {
            // test if using online sms provider or local modem
            if (ModemType == 1)
            {
                // local modem
                var modem = new SMSModem(ComPort);
                modem.Opens();
                modem.send(number, passcode, SmsC);
                modem.Closes();
            }
            else
            {
                LogDebug("Sending OTP: " + passcode + " to: " + number);

                // TODO: Use HttpUtility UrlEncode when we figure out how to add the dll!!!
                string url = Provider;
                url = url.Replace("***TEXTMESSAGE***", passcode);
                url = url.Replace("***NUMBER***", number);

                var client = new HttpClient();

                HttpResponseMessage response = client.GetAsync(url).Result;
                string content = response.Content.ReadAsStringAsync().Result;

                if (response.IsSuccessStatusCode)
                {
                    if (url.IndexOf("cpsms.dk") != -1)
                    {
                        // NOTE: Yes cpsms does indeed return HTTP 200 on errors!?!
                        if (content.IndexOf("error") != -1)
                            throw new SMSSendException(content);
                    }
                }
                else
                {
                    throw new SMSSendException(content);
                }
            }
        }


        public static string SendEmail(string email, string passcode)
        {
            var mail = new MailMessage();
            mail.To.Add(email);
            mail.From = new MailAddress(SenderEmail);
            mail.Subject = "Token: " + passcode;
            mail.Body = "Subject contains the token code to login to the site";
            mail.IsBodyHtml = false;

            var smtp = new SmtpClient(MailServer);

            try
            {
                smtp.Send(mail);
                if (DEBUG)
                    LogDebug(DateTime.Now + ": Mail sent to: " + email);
                return "SEND";
            }
            catch (InvalidCastException ex)
            {
                if (DEBUG)
                {
                    LogDebug(DateTime.Now + " : Debug: " + ex.Message);
                    LogDebug(DateTime.Now + " : Unable to send mail to: " + email +
                             "  ## Check that MAILSERVER and SENDEREMAIL are configured correctly in smscode.conf. Also check that your Webinterface server is allowed to relay through the mail server specified");
                }
                return "FAILED";
            }
        }


        public void cleanupEvent_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            RDSHandler.Cleanup();
        }
    }
}