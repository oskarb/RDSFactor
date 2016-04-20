using System;

namespace RDSFactor.Exceptions
{
    public class MissingNumber : Exception
    {
        public MissingNumber(string user)
            : base("User: " + user + " has no mobile number")
        {

        }
    }
}