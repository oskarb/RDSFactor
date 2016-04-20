using System;

public class MissingRadiusSecret : Exception
{
    public MissingRadiusSecret(string ip)
        : base("No shared secret for ip: " + ip + ". This MUST be inserted in the config file.")
    {

    }
}