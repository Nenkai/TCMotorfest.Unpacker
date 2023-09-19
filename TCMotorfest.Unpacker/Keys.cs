using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TCMotorfest.Unpacker.Crypto;

namespace TCMotorfest.Unpacker
{
    public class Keys
    {
        public static Dictionary<string, XTEAParameter> NamesToParams = new();

        public static List<XTEAParameter> XTEAKeyParameterTable = new();

        public static void LoadKeys()
        {
            if (!File.Exists("KeyTable.txt"))
            {
                throw new Exception("Key table file (KeyTable.txt) is missing");
            }

            var lines = File.ReadAllLines("KeyTable.txt");
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                string[] spl = line.Split('|');
                if (spl.Length != 3)
                    continue;

                if (!uint.TryParse(spl[0], out uint rounds))
                    continue;

                if (spl[1].StartsWith("0x"))
                    spl[1] = spl[1].Substring(2);

                if (!uint.TryParse(spl[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint decryptLength))
                    continue;

                var parameter = new XTEAParameter(rounds, decryptLength, spl[2]);
                XTEAKeyParameterTable.Add(parameter);
            }

            if (XTEAKeyParameterTable.Count != 32)
                throw new Exception("Expected 32 keys in KeyTable.txt");

            lines = File.ReadAllLines("FileToKey.txt");
            if (!File.Exists("FileToKey.txt"))
            {
                throw new Exception("File to key file (FileToKey.txt) is missing");
            }

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                string[] spl = line.Split('|');
                if (spl.Length != 4)
                    continue;

                if (!uint.TryParse(spl[1], out uint rounds))
                    continue;

                if (spl[2].StartsWith("0x"))
                    spl[2] = spl[2].Substring(2);

                if (!uint.TryParse(spl[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint decryptLength))
                    continue;

                var parameter = new XTEAParameter(rounds, decryptLength, spl[3]);
                NamesToParams.TryAdd(spl[0], parameter);
            }
        }
    }
}
