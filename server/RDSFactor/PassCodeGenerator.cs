using System;
using System.Collections.Generic;
using System.Linq;

namespace RDSFactor
{
    class PassCodeGenerator
    {
        public static string GenerateCode()
        {
            Random ordRand = new Random();
            var digits = new List<int>();

            // Generate a 6-digit code, with no single digit occurring more than twice as
            // a way to avoid accidentally generate "simple" codes like 001001, etc.

            while (digits.Count < Config.PassCodeLength)
            {
                var nextDigit = ordRand.Next(0, 9);
                if (digits.Count(d => d == nextDigit) < 2)
                    digits.Add(nextDigit);
            }

            var code = string.Join(string.Empty, digits.Select(i => i.ToString()));
            return code;
        }
    }
}