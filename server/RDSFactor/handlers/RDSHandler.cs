
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using RADAR;
using System.Web.Helpers;

namespace RDSFactor.handlers
{
    public class RDSHandler
    {
        /// <summary>
        /// User -> Token that proves user has authenticated, but not yet proved
        /// herself with the 2. factor
        /// </summary>
        private static Dictionary<string, string> authTokens = new Dictionary<string, string>();

        private static Dictionary<string, string> userSessions = new Dictionary<string, string>();
        private static Dictionary<string, DateTime> sessionTimestamps = new Dictionary<string, DateTime>();
        private static Dictionary<string, string> encryptedChallengeResults = new Dictionary<string, string>();
        private static Dictionary<string, DateTime> userLaunchTimestamps = new Dictionary<string, DateTime>();

        private RADIUSPacket mPacket;
        private string mUsername;
        private string mPassword;

        // RDS specific values 
        private bool mIsAppLaunchRequest;
        private bool mIsGatewayRequest;
        private bool mUseSMSFactor;
        private bool mUseEmailFactor;


        public RDSHandler(RADIUSPacket packet)
        {
            mPacket = packet;

            mUsername = mPacket.UserName;
            mPassword = mPacket.UserPassword;

            CleanUsername();

            foreach (var atts in mPacket.Attributes.GetAllAttributes(RadiusAttributeType.VendorSpecific))
            {
                string value = atts.GetVendorSpecific().VendorValue;

                switch (value.ToUpper())
                {
                    case "LAUNCH":
                        mIsAppLaunchRequest = true;
                        break;
                    case "TSGATEWAY":
                        mIsGatewayRequest = true;
                        break;
                    case "SMS":
                        mUseSMSFactor = true;
                        break;
                    case "EMAIL":
                        mUseEmailFactor = true;
                        break;
                }
            }
        }


        private void CleanUsername()
        {
            // RD Gateway sends EXAMPLE\username
            // RD Web sends example\username or - TODO - even example.com\username
            if (mUsername != null)
                mUsername = mUsername.ToLower();
        }


        public void ProcessRequest()
        {
            if (mIsAppLaunchRequest)
                ProcessAppLaunchRequest();
            else if (mIsGatewayRequest)
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
            RDSFactor.LogDebug(mPacket, "AppLaunchRequest");

            // When the packet is an AppLaunchRequest the password attribute contains the session id!
            var packetSessionId = mPassword;

            string storedSessionId;
            userSessions.TryGetValue(mUsername, out storedSessionId);

            if (storedSessionId == null)
            {
                RDSFactor.LogDebug(mPacket, "User has no session. MUST re-authenticate!");
                mPacket.RejectAccessRequest();
                return;
            }

            if (storedSessionId != packetSessionId)
            {
                RDSFactor.LogDebug(mPacket, "Stored session id didn't match packet session id!");
                mPacket.RejectAccessRequest();
                return;
            }

            if (HasValidSession(mUsername))
            {
                RDSFactor.LogDebug(mPacket, "Opening window");
                // Prolong user session
                sessionTimestamps[mUsername] = DateTime.Now;
                // Open gateway connection window
                userLaunchTimestamps[mUsername] = DateTime.Now;
                mPacket.AcceptAccessRequest();
                return;
            }
            else
            {
                RDSFactor.LogDebug(mPacket, "Session timed out -- User MUST re-authenticate");
                userSessions.Remove(mUsername);
                sessionTimestamps.Remove(mUsername);
                mPacket.RejectAccessRequest();
            }
        }


        public static bool HasValidLaunchWindow(string username)
        {
            DateTime timestamp;
            if (!userLaunchTimestamps.TryGetValue(username, out timestamp))
                return false;

            var secondsSinceLaunch = (DateTime.Now - timestamp).TotalSeconds;
            return secondsSinceLaunch < RDSFactor.LaunchTimeOut;
        }


        public static bool HasValidSession(string username)
        {
            string sessionID;
            userSessions.TryGetValue(username, out sessionID);

            DateTime timestamp;
            if (!sessionTimestamps.TryGetValue(username, out timestamp))
                return false;

            var minSinceLastActivity = (DateTime.Now - timestamp).TotalMinutes;
            return minSinceLastActivity < RDSFactor.SessionTimeOut;
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
            RDSFactor.LogDebug(mPacket, "Gateway Request");

            string sessionID;
            userSessions.TryGetValue(mUsername, out sessionID);

            DateTime launchTimestamp;
            userLaunchTimestamps.TryGetValue(mUsername, out launchTimestamp);

            if (sessionID == null || launchTimestamp == default(DateTime))
            {
                RDSFactor.LogDebug(mPacket, "User has no launch window. User must re-authenticate");
                mPacket.RejectAccessRequest();
            }


            var attributes = new RADIUSAttributes();

            var hasProxyState = mPacket.Attributes.AttributeExists(RadiusAttributeType.ProxyState);
            if (hasProxyState)
            {
                var proxyState = mPacket.Attributes.GetFirstAttribute(RadiusAttributeType.ProxyState);
                attributes.Add(proxyState);
            }

            if (HasValidLaunchWindow(mUsername))
            {
                RDSFactor.LogDebug(mPacket, "Opening gateway launch window");
                mPacket.AcceptAccessRequest(attributes);
            }
            else
            {
                RDSFactor.LogDebug(mPacket, "Gateway launch window has timed out!");
                mPacket.RejectAccessRequest();
            }

            RDSFactor.LogDebug(mPacket, "Removing gateway launch window");
            userLaunchTimestamps.Remove(mUsername);
        }


        public void ProcessAccessRequest()
        {
            var hasState = mPacket.Attributes.AttributeExists(RadiusAttributeType.State);
            if (hasState)
            {
                // An Access-Request with a state is pr. definition a challenge response.
                ProcessChallengeResponse();
                return;
            }

            RDSFactor.LogDebug(mPacket, "AccessRequest");
            try
            {
                var ldapResult = Authenticate();

                if (RDSFactor.EnableOTP)
                {
                    TwoFactorChallenge(ldapResult);
                    return;
                }
                else
                    Accept();
            }
            catch (Exception ex)
            {
                RDSFactor.LogDebug(mPacket, "Authentication failed. Sending reject. Error: " + ex.Message);
                mPacket.RejectAccessRequest(ex.Message);
            }
        }


        private void Accept()
        {
            RDSFactor.LogDebug(mPacket, "AcceptAccessRequest");
            var sGUID = Guid.NewGuid().ToString();

            userSessions[mUsername] = sGUID;
            sessionTimestamps[mUsername] = DateTime.Now;

            var attributes = new RADIUSAttributes();
            var guidAttribute = new RADIUSAttribute(RadiusAttributeType.ReplyMessage, sGUID);

            attributes.Add(guidAttribute);
            mPacket.AcceptAccessRequest(attributes);
        }


        private void ProcessChallengeResponse()
        {
            var authToken = mPacket.Attributes.GetFirstAttribute(RadiusAttributeType.State).ToString();
            string expectedAuthToken;

            if (!authTokens.TryGetValue(mUsername, out expectedAuthToken) || authToken != expectedAuthToken)
                throw new Exception("User is trying to respond to challenge without valid auth token");

            // When the packet is an Challenge-Response the password attr. contains the encrypted result
            var userEncryptedResult = mPassword;

            string localEncryptedResult;
            if (encryptedChallengeResults.TryGetValue(mUsername, out localEncryptedResult)
                && localEncryptedResult == userEncryptedResult)
            {
                RDSFactor.LogDebug(mPacket, "ChallengeResponse Success");
                encryptedChallengeResults.Remove(mUsername);
                authTokens.Remove(mUsername);
                Accept();
            }
            else
            {
                RDSFactor.LogDebug(mPacket, "Wrong challenge code!");
                mPacket.RejectAccessRequest();
            }
        }


        private void TwoFactorChallenge(SearchResult ldapResult)
        {
            var challengeCode = RDSFactor.GenerateCode();
            var authToken = System.Guid.NewGuid().ToString();
            var clientIP = mPacket.EndPoint.Address.ToString();

            RDSFactor.LogDebug(mPacket, "Access Challenge Code: " + challengeCode);

            string sharedSecret ;
            if (!RDSFactor.secrets.TryGetValue(clientIP, out sharedSecret))
                throw new Exception("No shared secret for client:" + clientIP);

            authTokens[mUsername]=authToken;
            string encryptedChallengeResult = Crypto.SHA256(mUsername + challengeCode + sharedSecret);
            encryptedChallengeResults[mUsername] = encryptedChallengeResult;

            if (mUseSMSFactor)
            {
                var mobile = LdapGetNumber(ldapResult);
                RDSFactor.SendSMS(mobile, challengeCode);
            }

            if (mUseEmailFactor)
            {
                var email = LdapGetEmail(ldapResult);
                RDSFactor.SendEmail(email, challengeCode);
            }


            var attributes = new RADIUSAttributes
            {
                new RADIUSAttribute(RadiusAttributeType.ReplyMessage, "SMS Token"),
                new RADIUSAttribute(RadiusAttributeType.State, authToken)
            };

            mPacket.SendAccessChallenge(attributes);
        }


        private SearchResult Authenticate()
        {
            var password = mPacket.UserPassword;
            var ldapDomain = RDSFactor.LDAPDomain;

            RDSFactor.LogDebug(mPacket, "Authenticating with LDAP: " + "LDAP://" + ldapDomain);
            DirectoryEntry dirEntry = new DirectoryEntry("LDAP://" + ldapDomain, mUsername, password);

            var obj = dirEntry.NativeObject;
            var search = new DirectorySearcher(dirEntry);

            if (mUsername.Contains("@"))
                search.Filter = "(userPrincipalName=" + mUsername + ")";
            else
            {
                var usernameParts = mUsername.Split('\\');
                search.Filter = "(SAMAccountName=" + usernameParts.Last() + ")";
            }

            search.PropertiesToLoad.Add("distinguishedName");
            if (RDSFactor.EnableOTP)
            {
                search.PropertiesToLoad.Add(RDSFactor.ADMobileField);
                search.PropertiesToLoad.Add(RDSFactor.ADMailField);
            }

            var result = search.FindOne();

            if (result == null)
            {
                RDSFactor.LogDebug(mPacket, "Failed to authenticate with Active Directory");
                throw new MissingUser();
            }

            return result;
        }


        private string LdapGetNumber(SearchResult result)
        {
            if (!result.Properties.Contains(RDSFactor.ADMobileField))
                throw new MissingLdapField(RDSFactor.ADMobileField, mUsername);

            string mobile = (string) result.Properties[RDSFactor.ADMobileField][0];
            mobile = mobile.Replace("+", "");

            if (string.IsNullOrWhiteSpace(mobile))
            {
                RDSFactor.LogDebug(mPacket, "Unable to find correct phone number for user " + mUsername);
                throw new MissingNumber(mUsername);
            }

            return mobile;
        }


        private string LdapGetEmail(SearchResult result)
        {
            if (!result.Properties.Contains(RDSFactor.ADMailField))
                throw new MissingLdapField(RDSFactor.ADMailField, mUsername);

            string email = (string) result.Properties[RDSFactor.ADMailField][0];
            if (!email.Contains("@"))
            {
                RDSFactor.LogDebug(mPacket, "Unable to find correct email for user " + mUsername);
                throw new MissingEmail(mUsername);
            }

            return email;
        }


        public static void Cleanup()
        {
            RDSFactor.LogDebug("TimerCleanUp");

            var users = userSessions.Keys.ToList();
            foreach (var username in users)
            {
                if (!HasValidSession(username))
                {
                    userSessions.Remove(username);
                    sessionTimestamps.Remove(username);
                    userLaunchTimestamps.Remove(username);
                    encryptedChallengeResults.Remove(username);
                    authTokens.Remove(username);
                }
            }
        }
    }
}
