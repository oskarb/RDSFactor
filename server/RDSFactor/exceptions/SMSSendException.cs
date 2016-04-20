using System;

public class SMSSendException : Exception
{
    public SMSSendException(string message)
        : base("SMS send error: " + message)
    {

    }
}