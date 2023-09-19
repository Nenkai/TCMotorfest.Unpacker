using Syroot.BinaryData;

using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Diagnostics;

namespace TCMotorfest.Unpacker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("TCMotorfestUnpack by Nenkai");
            Console.WriteLine("- https://github.com/Nenkai");
            Console.WriteLine("- https://twitter.com/Nenkaai");
            Console.WriteLine("-----------------------------");

            if (args.Length != 2)
            {
                Console.WriteLine("Usage: <toc file> <output dir>");
                return;
            }

            try
            {
                Keys.LoadKeys();

                var bigFile = new BigFileSystem();
                bigFile.Init(args[0]);
                bigFile.ExtractAll(args[1]);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Errored: {e}");
            }
        }
    }
}