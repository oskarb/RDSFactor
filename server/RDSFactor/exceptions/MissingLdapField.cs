using System;

public class MissingLdapField : Exception
{
    public MissingLdapField(string field, string username)
        : base("No " + field + " entry in LDAP for " + username)
    {
    }
}