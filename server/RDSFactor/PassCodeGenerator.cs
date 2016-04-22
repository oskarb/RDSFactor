using System;
using System.Linq;

namespace RDSFactor
{
    class PassCodeGenerator
    {
        public static string GenerateCode()
        {
            Random ordRand = new Random();
            int[] temp = new int[6];

            for (int i = 0; i < temp.Length; i++)
            {
                var dummy = ordRand.Next(1, 9);
                if (!temp.Contains(dummy))
                {
                    temp[i] = dummy;
                }
            }

            var code = string.Join(string.Empty, temp.Select(i => i.ToString()));
            return code;
        }
    }
}