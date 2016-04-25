using System.Collections.Generic;
using RADAR;

namespace RDSFactor
{
    class Config
    {
        public static string LDAPDomain = "";
        public static List<string> ADPhoneAttributes = new List<string>();
        public static string ADMailAttribute = "";
        public static bool EnableOTP;
        public static int SessionTimeOut = 30; // in minutes
        public static int LaunchTimeOut = 60; // in seconds
        public static NASAuthList Secrets = new NASAuthList();
    }
}