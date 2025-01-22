using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using TCMotorfest.Unpacker.Crypto;
using TCMotorfest.Unpacker.Compression;
using System.IO;
using System.Numerics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TCMotorfest.Unpacker;

/// <summary>
/// TCMotorfest big file system. (Disposable object)
/// </summary>
public class CbdFile
{
    /// <summary>
    /// "CBD ".
    /// </summary>
    public const uint MAGIC = 0x20444243;

    public Dictionary<string, byte[]> Files { get; set; } = [];

    public void FromStream(Stream stream)
    {
        BinaryStream bs = new BinaryStream(stream, ByteConverter.Little);
        if (bs.ReadUInt32() != MAGIC)
            throw new InvalidDataException("Not a .cbd file. Magic did not match.");

        uint version = bs.ReadUInt32();

        while (!bs.EndOfStream)
        {
            string str = bs.ReadString(StringCoding.Int16CharCount);
            bs.Align(0x04);

            uint fileSize = bs.ReadUInt32();
            bs.Align(0x10);

            Debug.Assert(bs.Position + fileSize <= bs.Length, $"File inside cbd '{str}' exceeds container file size ({bs.Position:X} + 0x{fileSize:X} > 0x{bs.Length:X})");

            byte[] fileData = bs.ReadBytes((int)fileSize);
            Files.Add(str, fileData);
        }
    }
}
