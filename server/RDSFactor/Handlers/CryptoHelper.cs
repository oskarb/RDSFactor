using System;
using System.Security.Cryptography;
using System.Text;

namespace RDSFactor.Handlers
{
    public class CryptoHelper
    {
        /// <summary>
        /// Return the SHA256 hash of the input as a hexadecimal string. The input string is encoded
        /// using UTF-16 before hashing.
        /// </summary>
        public static string SHA256(string input)
        {
            var hasher = new SHA256Managed();

            byte[] inputBytes = Encoding.Unicode.GetBytes(input);
            var hashBytes = hasher.ComputeHash(inputBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "");
        }
    }
}