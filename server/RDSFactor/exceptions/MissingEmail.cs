using System;

namespace RDSFactor.Exceptions
{
    public class MissingEmail : Exception
    {
        public MissingEmail(string user)
            : base("User: " + user + " has no email")
        {

        }
    }
}