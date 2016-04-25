using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
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
        public static string DefaultNumberPrefix { get; set; }

        public static string HttpBasicAuthUserPassword { get; set; }
        public static bool UseHttpPost { get; set; }
        private static Dictionary<string, string> _requestParameters = new Dictionary<string, string>();

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

        public static IDictionary<string, string> RequestParameters
        {
            get { return _requestParameters; }
        }


        public static void SendSMS(string number, string passcode)
        {
            // If a default prefix has been configured, try to cleanup the number
            // and add the prefix if the number doesn't already start with a +.
            if (!string.IsNullOrWhiteSpace(DefaultNumberPrefix))
            {
                number = number.Trim().TrimStart('0');
                if (!number.StartsWith("+"))
                    number = DefaultNumberPrefix + number;
            }

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
                Logger.LogDebug("Begin HTTP call to send SMS pass code.");

                string url = _provider;

                // The Provider setting may contain request parameters that should be replaced. This
                // feature is retained for convenience and backwards compatibility, although further
                // down we can add additional parameters.
                url = url.Replace("***TEXTMESSAGE***", WebUtility.UrlEncode(passcode))
                         .Replace("***NUMBER***", WebUtility.UrlEncode(number));

                // Prepare request parameters. These will either be used as POST content
                // or tacked on to the query string for GET.
                var requestParams = new List<KeyValuePair<string, string>>();
                foreach (var reqParameter in RequestParameters)
                {
                    string value = reqParameter.Value;
                    value = value.Replace("***TEXTMESSAGE***", passcode)
                                 .Replace("***NUMBER***", number);
                    requestParams.Add(new KeyValuePair<string, string>(reqParameter.Key, value));
                }

                using (var client = new HttpClient())
                {
                    // HTTP Basic authentication, if configured.
                    if (!string.IsNullOrWhiteSpace(HttpBasicAuthUserPassword))
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(HttpBasicAuthUserPassword)));

                    HttpResponseMessage response;
                    if (UseHttpPost)
                    {
                        var postContent = new FormUrlEncodedContent(requestParams);
                        response = client.PostAsync(_provider, postContent).Result;
                    }
                    else
                    {
                        var urlEncParts =
                            requestParams.Select(param => param.Key + "=" + WebUtility.UrlEncode(param.Value));
                        var additionalQueryString = string.Join("&", urlEncParts);
                        if (additionalQueryString != "" && !url.Contains("?"))
                            additionalQueryString = "?" + additionalQueryString;

                        response = client.GetAsync(url + additionalQueryString).Result;
                    }

                    string content = response.Content.ReadAsStringAsync().Result;

                    if (response.IsSuccessStatusCode)
                    {
                        if (url.IndexOf("cpsms.dk") != -1 && content.IndexOf("error") != -1)
                            throw new SMSSendException(content);

                        LogPassCodeSentSuccessfully(passcode, "SMS", number);
                    }
                    else
                    {
                        throw new SMSSendException(content);
                    }
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
                LogPassCodeSentSuccessfully(passcode, "EMAIL", email);
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


        private static void LogPassCodeSentSuccessfully(string passcode, string method, string destination)
        {
            Logger.LogInfo($"One time password {passcode} sent to: ({method}) {destination}");
        }
    }
}