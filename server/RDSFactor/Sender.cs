using System;
using System.Net.Http;
using System.Net.Mail;
using RDSFactor.Exceptions;

namespace RDSFactor
{
    class Sender
    {
        private static string _provider = "";
        private static ModemType _modemType = ModemType.Internet;
        private static string _comPort = "";
        private static string _smsC = "";
        private static string _mailServer = "";
        private static string _senderEmail = "";

        public static string Provider
        {
            set { _provider = value; }
            get { return _provider; }
        }

        public static ModemType ModemType
        {
            set { _modemType = value; }
            get { return _modemType; }
        }

        public static string ComPort
        {
            set { _comPort = value; }
            get { return _comPort; }
        }

        public static string SmsC
        {
            set { _smsC = value; }
            get { return _smsC; }
        }

        public static string MailServer
        {
            set { _mailServer = value; }
            get { return _mailServer; }
        }

        public static string SenderEmail
        {
            set { _senderEmail = value; }
            get { return _senderEmail; }
        }

        public static void SendSMS(string number, string passcode)
        {
            // test if using online sms provider or local modem
            if (_modemType == ModemType.SmsModem)
            {
                // local modem
                var modem = new SMSModem(_comPort);
                modem.Opens();
                modem.Send(number, passcode, _smsC);
                modem.Closes();
            }
            else
            {
                Logger.LogDebug("Sending OTP: " + passcode + " to: " + number);

                // TODO: Use HttpUtility UrlEncode when we figure out how to add the dll!!!
                string url = _provider;
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

        public static bool SendEmail(string email, string passcode)
        {
            var mail = new MailMessage();
            mail.To.Add(email);
            mail.From = new MailAddress(_senderEmail);
            mail.Subject = "Token: " + passcode;
            mail.Body = "Subject contains the token code to login to the site";
            mail.IsBodyHtml = false;

            var smtp = new SmtpClient(_mailServer);

            try
            {
                smtp.Send(mail);
                Logger.LogDebug("Mail sent to: " + email);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unable to send mail to {email}: {ex.Message}");
                Logger.LogError("  ## Check that MAILSERVER and SENDEREMAIL are configured correctly." +
                                " Also check that your server is allowed to relay through the mail server specified.");

                return false;
            }
        }
    }
}