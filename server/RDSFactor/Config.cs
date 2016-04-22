using RADAR;

namespace RDSFactor
{
    class Config
    {
        public static string LDAPDomain = "";
        public static string ADMobileField = "";
        public static string ADMailField = "";
        public static bool EnableOTP;
        public static int SessionTimeOut = 30; // in minutes
        public static int LaunchTimeOut = 30; // in seconds
        public static NASAuthList Secrets = new NASAuthList();
    }
}