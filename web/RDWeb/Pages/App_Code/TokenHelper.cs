using System;
using System.Configuration;
using System.Web;

/// <summary>
/// Methods for tracking state of two-factor authentication.
/// </summary>
public static class TokenHelper
{
    public static bool UseTwoFactorAuthentication()
    {
        string strSmsToken = ConfigurationManager.AppSettings["SmsToken"];
        if (string.IsNullOrWhiteSpace(strSmsToken))
            return false;

        bool use2;
        if (!bool.TryParse(strSmsToken.Trim(), out use2))
            throw new FormatException("Unable to parse value for setting SmsToken.");

        return use2;
    }


    public static bool IsTwoFactorAuthSatisfied()
    {
        // 2FA is satisfied if the user is phase-1 authenticated and 2FA
        // is either turned off, or have also been successfully completed.

        if (!HttpContext.Current.User.Identity.IsAuthenticated)
            return false;

        if (!UseTwoFactorAuthentication())
            return true;

        return "SMS_AUTH".Equals(HttpContext.Current.Session["SMSTOKEN"]);
    }


    public static void SetTwoFactorValidated(bool isValid)
    {
        HttpContext.Current.Session["SMSTOKEN"] = isValid ? "SMS_AUTH" : null;
    }
}