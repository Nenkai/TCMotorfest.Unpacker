using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCMotorfest.Unpacker
{
    public static class Utils
    {
        public static string MagicToString(uint magic)
        {
            string hex = magic.ToString("X4");

            StringBuilder sb = new StringBuilder();
            for (int i = hex.Length - 2; i >= 0; i -= 2)
            {
                string hs = hex.Substring(i, 2);
                sb.Append(Convert.ToChar(Convert.ToUInt32(hs, 16)));
            }

            return sb.ToString();
        }
    }
}
