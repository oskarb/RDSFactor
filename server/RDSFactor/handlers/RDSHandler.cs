using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using RADAR;
using RDSFactor.Exceptions;

namespace RDSFactor.Handlers
{
    public class RDSHandler
    {
        /// <summary>
        /// User -> Token that proves user has authenticated, but not yet proved
        /// herself with the 2. factor
        /// </summary>
        private static readonly Dictionary<string, string> AuthTokens = new Dictionary<string, string>();

        private static readonly Dictionary<string, string> UserSessions = new Dictionary<string, string>();
        private static readonly Dictionary<string, DateTime> SessionTimestamps = new Dictionary<string, DateTime>();
        private static readonly Dictionary<string, string> EncryptedChallengeResults = new Dictionary<string, string>();
        private static readonly Dictionary<string, DateTime> UserLaunchTimestamps = new Dictionary<string, DateTime>();

        private readonly RADIUSPacket _packet;
        private readonly string _username;
        private readonly string _password;

        // RDS specific values 
        private readonly bool _isAppLaunchRequest;
        private readonly bool _isGatewayRequest;
        private readonly bool _useSmsFactor;
        private readonly bool _useEmailFactor;


        public RDSHandler(RADIUSPacket packet)
        {
            _packet = packet;

            _username = CleanUsername(_packet.UserName);
            _password = _packet.UserPassword;
            

            foreach (var atts in _packet.Attributes.GetAllAttributes(RadiusAttributeType.VendorSpecific))
            {
                string value = atts.GetVendorSpecific().VendorValue;

                switch (value.ToUpper())
                {
                    case "LAUNCH":
                        _isAppLaunchRequest = true;
                        break;
                    case "TSGATEWAY":
                        _isGatewayRequest = true;
                        break;
                    case "SMS":
                        _useSmsFactor = true;
                        break;
                    case "EMAIL":
                        _useEmailFactor = true;
                        break;
                }
            }
        }


        private static string CleanUsername(string userName)
        {
            // RD Gateway sends EXAMPLE\username
            // RD Web sends example\username or - TODO - even example.com\username
            userName = userName?.ToLower();

            return userName;
        }


        public void ProcessRequest()
        {
            if (_isAppLaunchRequest)
                ProcessAppLaunchRequest();
            else if (_isGatewayRequest)
                ProcessGatewayRequest();
            else
                ProcessAccessRequest();
        }


        /// <summary>
        /// Process the RDS specific App Launch request.
        /// These requests are sent when an app is clicked in RD Web.
        /// 
        /// It's checked whether the session is still valid.In which case, a
        /// window is opened for the user, where we allow the user to connect
        /// through the gateway, an Accept-Access is returned and the RD Web
        /// launches the RDP client.
        ///
        /// NOTE: Requests contain the session GUID in the password attribute
        /// of the packet.
        /// </summary>
        public void ProcessAppLaunchRequest()
        {
            Logger.LogDebug(_packet, "AppLaunchRequest");

            // When the packet is an AppLaunchRequest the password attribute contains the session id!
            var packetSessionId = _password;

            string storedSessionId;
            UserSessions.TryGetValue(_username, out storedSessionId);

            if (storedSessionId == null)
            {
                Logger.LogDebug(_packet, "User has no session. MUST re-authenticate!");
                _packet.RejectAccessRequest();
                return;
            }

            if (storedSessionId != packetSessionId)
            {
                Logger.LogDebug(_packet, "Stored session id didn't match packet session id!");
                _packet.RejectAccessRequest();
                return;
            }

            if (HasValidSession(_username))
            {
                Logger.LogDebug(_packet, "Opening window");
                // Prolong user session
                SessionTimestamps[_username] = DateTime.Now;
                // Open gateway connection window
                UserLaunchTimestamps[_username] = DateTime.Now;
                _packet.AcceptAccessRequest();
            }
            else
            {
                Logger.LogDebug(_packet, "Session timed out -- User MUST re-authenticate");
                UserSessions.Remove(_username);
                SessionTimestamps.Remove(_username);
                _packet.RejectAccessRequest();
            }
        }


        public static bool HasValidLaunchWindow(string username)
        {
            DateTime timestamp;
            if (!UserLaunchTimestamps.TryGetValue(username, out timestamp))
                return false;

            var secondsSinceLaunch = (DateTime.Now - timestamp).TotalSeconds;
            return secondsSinceLaunch < Config.LaunchTimeOut;
        }


        public static bool HasValidSession(string username)
        {
            //string sessionId;
            //UserSessions.TryGetValue(username, out sessionId);

            DateTime timestamp;
            if (!SessionTimestamps.TryGetValue(username, out timestamp))
                return false;

            var minSinceLastActivity = (DateTime.Now - timestamp).TotalMinutes;
            return minSinceLastActivity < Config.SessionTimeOut;
        }


        /// <summary>
        /// Process the request from the Network Policy Server in the RDS Gateway.
        /// These are sent when an RDP client tries to connect through the Gateway.
        ///
        /// Accept-Access is returned when the user has a
        /// * valid session; and a
        /// * valid app launch window
        ///
        /// The launch window is closed after this request.
        /// </summary>
        public void ProcessGatewayRequest()
        {
            Logger.LogDebug(_packet, "Gateway Request");

            string sessionId;
            UserSessions.TryGetValue(_username, out sessionId);

            DateTime launchTimestamp;
            UserLaunchTimestamps.TryGetValue(_username, out launchTimestamp);

            if (sessionId == null || launchTimestamp == default(DateTime))
            {
                Logger.LogDebug(_packet, "User has no launch window. User must re-authenticate");
                _packet.RejectAccessRequest();
            }


            var attributes = new RADIUSAttributes();

            var hasProxyState = _packet.Attributes.AttributeExists(RadiusAttributeType.ProxyState);
            if (hasProxyState)
            {
                var proxyState = _packet.Attributes.GetFirstAttribute(RadiusAttributeType.ProxyState);
                attributes.Add(proxyState);
            }

            if (HasValidLaunchWindow(_username))
            {
                Logger.LogDebug(_packet, "Opening gateway launch window");
                _packet.AcceptAccessRequest(attributes);
            }
            else
            {
                Logger.LogDebug(_packet, "Gateway launch window has timed out!");
                _packet.RejectAccessRequest();
            }

            Logger.LogDebug(_packet, "Removing gateway launch window");
            UserLaunchTimestamps.Remove(_username);
        }


        public void ProcessAccessRequest()
        {
            var hasState = _packet.Attributes.AttributeExists(RadiusAttributeType.State);
            if (hasState)
            {
                // An Access-Request with a state is pr. definition a challenge response.
                ProcessChallengeResponse();
                return;
            }

            Logger.LogDebug(_packet, "AccessRequest");
            try
            {
                var ldapResult = Authenticate();

                if (Config.EnableOTP)
                    TwoFactorChallenge(ldapResult);
                else
                    Accept();
            }
            catch (Exception ex)
            {
                Logger.LogDebug(_packet, "Authentication failed. Sending reject. Error: " + ex.Message);
                _packet.RejectAccessRequest(ex.Message);
            }
        }


        private void Accept()
        {
            Logger.LogDebug(_packet, "AcceptAccessRequest");
            var sGuid = Guid.NewGuid().ToString();

            UserSessions[_username] = sGuid;
            SessionTimestamps[_username] = DateTime.Now;

            var attributes = new RADIUSAttributes();
            var guidAttribute = new RADIUSAttribute(RadiusAttributeType.ReplyMessage, sGuid);

            attributes.Add(guidAttribute);
            _packet.AcceptAccessRequest(attributes);
        }


        private void ProcessChallengeResponse()
        {
            var authToken = _packet.Attributes.GetFirstAttribute(RadiusAttributeType.State).ToString();
            string expectedAuthToken;

            if (!AuthTokens.TryGetValue(_username, out expectedAuthToken) || authToken != expectedAuthToken)
                throw new Exception("User is trying to respond to challenge without valid auth token");

            // When the packet is an Challenge-Response the password attr. contains the encrypted result
            var userEncryptedResult = _password;

            string localEncryptedResult;
            if (EncryptedChallengeResults.TryGetValue(_username, out localEncryptedResult)
                && localEncryptedResult == userEncryptedResult)
            {
                Logger.LogDebug(_packet, "ChallengeResponse Success");
                EncryptedChallengeResults.Remove(_username);
                AuthTokens.Remove(_username);
                Accept();
            }
            else
            {
                Logger.LogDebug(_packet, "Wrong challenge code!");
                _packet.RejectAccessRequest();
            }
        }


        private void TwoFactorChallenge(SearchResult ldapResult)
        {
            string challengeCode = PassCodeGenerator.GenerateCode();
            string authToken = Guid.NewGuid().ToString();
            string clientIp = _packet.EndPoint.Address.ToString();

            Logger.LogDebug(_packet, "Access Challenge Code: " + challengeCode);

            string sharedSecret ;
            if (!Config.Secrets.TryGetValue(clientIp, out sharedSecret))
                throw new Exception("No shared secret for client:" + clientIp);

            AuthTokens[_username]=authToken;
            string encryptedChallengeResult = CryptoHelper.SHA256(_username + challengeCode + sharedSecret);
            EncryptedChallengeResults[_username] = encryptedChallengeResult;

            if (_useSmsFactor)
            {
                var mobile = LdapGetNumberCleaned(ldapResult);
                Sender.SendSMS(mobile, challengeCode);
            }

            if (_useEmailFactor)
            {
                var email = LdapGetEmail(ldapResult);
                Sender.SendEmail(email, challengeCode);
            }


            var attributes = new RADIUSAttributes
            {
                new RADIUSAttribute(RadiusAttributeType.ReplyMessage, "SMS Token"),
                new RADIUSAttribute(RadiusAttributeType.State, authToken)
            };

            _packet.SendAccessChallenge(attributes);
        }


        private SearchResult Authenticate()
        {
            var password = _packet.UserPassword;
            var ldapDomain = Config.LDAPDomain;

            Logger.LogDebug(_packet, "Authenticating with LDAP: " + "LDAP://" + ldapDomain);
            DirectoryEntry dirEntry = new DirectoryEntry("LDAP://" + ldapDomain, _username, password);

            //var obj = dirEntry.NativeObject;
            var search = new DirectorySearcher(dirEntry);

            if (_username.Contains("@"))
                search.Filter = "(userPrincipalName=" + _username + ")";
            else
            {
                var usernameParts = _username.Split('\\');
                search.Filter = "(SAMAccountName=" + usernameParts.Last() + ")";
            }

            search.PropertiesToLoad.Add("distinguishedName");
            if (Config.EnableOTP)
            {
                foreach (var adAttribute in Config.ADPhoneAttributes)
                    search.PropertiesToLoad.Add(adAttribute);
                search.PropertiesToLoad.Add(Config.ADMailField);
            }

            var result = search.FindOne();

            if (result == null)
            {
                Logger.LogDebug(_packet, "Failed to authenticate with Active Directory");
                throw new MissingUser();
            }

            return result;
        }


        private string LdapGetNumberCleaned(SearchResult result)
        {
            string mobile = null;

            // Iterate over configured attributes in order, and use the first
            // value that looks reasonable.
            foreach (var adAttribute in Config.ADPhoneAttributes)
            {
                if (result.Properties.Contains(adAttribute))
                    mobile = (string) result.Properties[adAttribute][0];

                // For now, I'll retain this line of code from earlier versions, though I'm
                // not sure why + characters are being removed. If this is for international
                // dialing prefix, we should probably replace with the correct prefix instead,
                // or perhaps not at all if the SMS service handles it.
                mobile = mobile?.Replace("+", "");

                mobile = mobile?.Replace(" ", "").Replace("-", "");

                // If there is anything left after above cleanup, use this number.
                if (!string.IsNullOrWhiteSpace(mobile))
                    break;
            }

            if (string.IsNullOrWhiteSpace(mobile))
            {
                Logger.LogDebug(_packet, "Unable to find any phone number for user " + _username);
                throw new MissingNumber(_username);
            }

            return mobile;
        }


        private string LdapGetEmail(SearchResult result)
        {
            if (!result.Properties.Contains(Config.ADMailField))
                throw new MissingLdapField(Config.ADMailField, _username);

            string email = (string) result.Properties[Config.ADMailField][0];
            if (!email.Contains("@"))
            {
                Logger.LogDebug(_packet, "Unable to find correct email for user " + _username);
                throw new MissingEmail(_username);
            }

            return email;
        }


        public static void Cleanup()
        {
            Logger.LogDebug("TimerCleanUp");

            var users = UserSessions.Keys.ToList();
            foreach (var username in users)
            {
                if (!HasValidSession(username))
                {
                    UserSessions.Remove(username);
                    SessionTimestamps.Remove(username);
                    UserLaunchTimestamps.Remove(username);
                    EncryptedChallengeResults.Remove(username);
                    AuthTokens.Remove(username);
                }
            }
        }
    }
}
