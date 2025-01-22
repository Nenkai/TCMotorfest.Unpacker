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

namespace TCMotorfest.Unpacker;

/// <summary>
/// TCMotorfest big file system. (Disposable object)
/// </summary>
public class BigFileSystem : IDisposable
{
    public SectionInfo MainTocSection { get; set; }
    public Dictionary<ulong, string> HashToPath { get; set; } = new();

    public uint DataEncryptionMethod { get; set; } = 0x4E4F4E45; // Default to NONE
    public uint DataCompressionMethod { get; set; } = 0x4E4F4E45; // Default to NONE

    /// <summary>
    /// XTEA Key for the current big file.
    /// </summary>
    public XTEAParameter XTeaKey { get; set; }

    /// <summary>
    /// Unused
    /// </summary>
    public string XorKey { get; set; }

    public uint UnkVersion1 { get; set; }
    public uint NumBanksMaybe { get; set; }
    public uint NumRDCMaybe { get; set; }
    public uint UnkVersion4 { get; set; }
    public DateTimeOffset Date { get; set; }

    private string _bigFileName;
    private string _bigDataFileName;
    private string _filePath;

    public List<Bank> Banks { get; } = new();

    public void Init(string file)
    {
        _filePath = file;
        _bigFileName = Path.GetFileNameWithoutExtension(file);

        var lines = File.ReadAllLines("FileNames.txt");
        foreach (var line in lines)
        {
            string path = line;
            string[] spl = line.Split(" ");
            if (spl.Length == 2)
                path = spl[1];

            HashToPath.TryAdd(HashPath(path), path);
        }

        using var fs = new FileStream(file, FileMode.Open);
        using var bs = new BinaryStream(fs, ByteConverter.Little);

        Console.WriteLine("Parsing TOC file sections..");
        MainTocSection = InitSections(bs);
        ParseSection(bs, MainTocSection);

        Console.WriteLine($"- VRSN Value 1: {UnkVersion1}");
        Console.WriteLine($"- Num Banks (?): {NumBanksMaybe}");
        Console.WriteLine($"- Num RDC (?): {NumRDCMaybe}");
        Console.WriteLine($"- VRSN Value: {Utils.MagicToString(UnkVersion4)}");
        Console.WriteLine($"- Encryption: {Utils.MagicToString(DataEncryptionMethod)}");
        Console.WriteLine($"- Compression: {Utils.MagicToString(DataCompressionMethod)}");
        Console.WriteLine($"- Date: {Date}");

        _bigDataFileName = Path.ChangeExtension(file, ".bfd");
        if (!File.Exists(_bigDataFileName))
            throw new FileNotFoundException("Big file data (.bfd) is missing alongside toc file");

        Console.WriteLine("Big File TOC loaded");
        Console.WriteLine();
    }

    public void DumpAllHashes()
    {
        StreamWriter sw = new StreamWriter("Hashes/" + _bigFileName + ".txt");

        foreach (var bank in Banks)
        {
            sw.WriteLine($"Bank [{bank.BigFileIndex}]");

            foreach (var file in bank.FileInfos)
                sw.WriteLine($"{file.Value.Hash:X16}");
            sw.WriteLine();
        }
        sw.Dispose();
    }

    public void ExtractAll(string outputDir)
    {
        Console.WriteLine("Extracting files...");

        foreach (var bank in Banks)
        {
            string bigFileDataPath = Path.ChangeExtension(_bigDataFileName, bank.UsesSideBigFileMaybe == 1 ? $"b{bank.BigFileIndex:D2}" : "bfd");
            string bigFileName = Path.GetFileName(bigFileDataPath);

            int i = 0;
            foreach (var info in bank.FileInfos.Values)
            {
                string outputName;
                if (HashToPath.TryGetValue(info.Hash, out string? value))
                {
                    outputName = value;
                    Console.WriteLine($"[{i+1}/{bank.FileInfos.Count} {bigFileName}] Unpacking: {outputName}");
                }
                else
                {
                    outputName = $".unmapped/{info.Hash:X16}.bin";
                    Console.WriteLine($"[{i+1}/{bank.FileInfos.Count}] {bigFileName} Unpacking unmapped: {info.Hash:X16}");
                }

                string outputPath = Path.Combine(outputDir, outputName);
                ExtractFile(bank, info, outputPath);
                i++;
            }
        }
    }

    /// <summary>
    /// Extracts a file by hash
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public bool ExtractFile(ulong hash, string outputDir)
    {
        foreach (var bank in Banks)
        {
            if (bank.FileInfos.TryGetValue(hash, out FileInfo info))
            {
                string outputName;
                if (HashToPath.TryGetValue(info.Hash, out string? value))
                    outputName = value;
                else
                    outputName = $".unmapped/{info.Hash:X16}.bin";

                string outputPath = Path.Combine(outputDir, outputName);

                ExtractFile(bank, info, outputPath);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts a file by hash
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public bool ExtractFile(string path, string outputDir)
    {
        ulong hash = HashPath(path);
        return ExtractFile(hash, outputDir);
    }

    /// <summary>
    /// Extracts a file
    /// </summary>
    /// <param name="fileInfo"></param>
    public void ExtractFile(Bank bank, FileInfo fileInfo, string outputPath)
    {
        if (!bank.Initialized)
        {
            string bigFileDataPath = Path.ChangeExtension(_bigDataFileName, bank.UsesSideBigFileMaybe == 1 ? $"b{bank.BigFileIndex:D2}" : "bfd");
            string bigFileName = Path.GetFileName(_bigDataFileName);
            Console.WriteLine($"Loading bank '{bigFileName}'...");

            bank.InitStream(bigFileDataPath);
        }

        bank.Stream.Position = (long)fileInfo.Offset;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        byte[] fileData = new byte[fileInfo.CompressedSize];
        bank.Stream.Read(fileData, 0, (int)fileInfo.CompressedSize);

        if (fileInfo.Flags.HasFlag(FileFlags.Encrypted))
            Decrypt(DataEncryptionMethod, fileData, fileData, (uint)fileData.Length, XorKey, XTeaKey);

        if (fileInfo.Flags.HasFlag(FileFlags.Compressed))
        {
            byte[] decompressed = new byte[fileInfo.Size];
            if (!Decompress(DataCompressionMethod, fileData, decompressed, fileInfo.Size))
                throw new InvalidDataException("Failed to decompress file.");

            fileData = decompressed;
        }

        File.WriteAllBytes(outputPath, fileData);

        /*
        byte[] buffer = ArrayPool<byte>.Shared.Rent(0x20000);
        while (rem > 0)
        {
            int toRead = (int)Math.Min(rem, 0x20000);
            _bfdStream.Read(buffer, 0, toRead);
            output.Write(buffer, 0, toRead);

            rem -= toRead;
        }
        */
    }

    /// <summary>
    /// Inits big file sections from the provided stream
    /// </summary>
    /// <param name="bs"></param>
    /// <returns></returns>
    public SectionInfo InitSections(BinaryStream bs)
    {
        // header, version, size, data[size], section count

        var section = new SectionInfo();
        section.Magic = bs.ReadUInt32();
        section.Version = bs.ReadUInt32();
        section.SectionDataSize = bs.ReadUInt32();
        section.SectionDataOffset = bs.Position;
        bs.Position += section.SectionDataSize;
        section.SectionCount = bs.ReadUInt32();

        if (section.SectionCount >= 6)
            return section;

        for (int i = 0; i < section.SectionCount; i++)
        {
            SectionInfo childSection = InitSections(bs);
            section.Child.Add(childSection);
        }

        return section;
    }

    /// <summary>
    /// Parses big file sections from the provided stream
    /// </summary>
    /// <param name="bs"></param>
    /// <returns></returns>
    public bool ParseSection(BinaryStream bs, SectionInfo sec)
    {
        bs.Position = sec.SectionDataOffset;

        if (sec.Magic == 0x21434F54) // TOC!
        {
            Console.WriteLine("Entering main TOC! section");

            Debug.Assert(sec.Version == 0x2000000);

            if (sec.SectionCount == 0)
                return true;

            for (int i = 0; i < sec.SectionCount; i++)
            {
                bool res = ParseSection(bs, sec.Child[i]);
                if (!res)
                    break;
            }
        }
        else if (sec.Magic == 0x4E535256) // VRSN - Version
        {
            Console.WriteLine("Entering VRSN section");

            UnkVersion1 = bs.ReadUInt32();
            NumBanksMaybe = bs.ReadUInt32();
            NumRDCMaybe = bs.ReadUInt32();
            UnkVersion4 = bs.ReadUInt32(); // PDM5
            DataCompressionMethod = bs.ReadUInt32();
            DataEncryptionMethod = bs.ReadUInt32();
            Date = DateTimeOffset.FromUnixTimeSeconds(bs.ReadInt64());
        }
        else if (sec.Magic == 0x434F5445) // ETOC - Encrypted TOC
        {
            uint encryptionMethod = bs.ReadUInt32() ^ 0x55555555;
            uint encryptedDataLength = bs.ReadUInt32() ^ 0x55555555;
            Console.WriteLine($"ETOC (Encrypted TOC) found, decrypting.. (method: {Utils.MagicToString(encryptionMethod)})");

            if (encryptionMethod == 0x41455458)
            {
                if (!Keys.NamesToParams.TryGetValue(_bigFileName + ".toc", out XTEAParameter keyParams))
                    throw new Exception($"Failed to find decryption key for {_bigFileName} - make sure not to rename the .toc file from the original.");

                Console.WriteLine($"Using XTEA Key: {keyParams.Key} ({_bigFileName})");

                XTeaKey = keyParams;
            }

            byte[] data = bs.ReadBytes((int)encryptedDataLength);

            // Bruteforce
            /*
            int keyIndex = FindKey(data);
            if (keyIndex == -1)
                throw new Exception("Encryption key to use could not be determined - failed to decrypt"); */

            //if (_bigFileName == "localization")
            //    XTeaKey.DecryptSize = encryptedDataLength;

            Decrypt(encryptionMethod, data, data, encryptedDataLength, "", XTeaKey);

            using var subTocStream = new MemoryStream(data);
            using var subBinaryStream = new BinaryStream(subTocStream);

            var subSection = InitSections(subBinaryStream);
            ParseSection(subBinaryStream, subSection);
        }
        else if (sec.Magic == 0x434F5443) // CTOC - CompressedToc
        {
            uint compressionMethod = bs.ReadUInt32() ^ 0x55555555; // KRKN
            uint compressedSize = bs.ReadUInt32() ^ 0x55555555;
            uint decompressedSize = bs.ReadUInt32() ^ 0x55555555;
            Console.WriteLine($"CTOC (Compressed TOC) found, decompressing.. (method: {Utils.MagicToString(compressionMethod)})");

            byte[] compressedData = bs.ReadBytes((int)compressedSize);
            byte[] decompressedBuffer = new byte[decompressedSize];
            if (!Decompress(compressionMethod, compressedData, decompressedBuffer, decompressedSize))
                throw new Exception("Failed to decompress TOC.");

            using var subTocStream = new MemoryStream(decompressedBuffer);
            using var subBinaryStream = new BinaryStream(subTocStream);

            var subSection = InitSections(subBinaryStream);
            ParseSection(subBinaryStream, subSection);
        }
        else if (sec.Magic == 0x21434452) // RDC!
        {
            Console.WriteLine("Entering RDC! section (unimplemented)");
            string og = bs.ReadString(StringCoding.ZeroTerminated);
            bs.Align(0x04);

            int count = bs.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                ulong hash = bs.ReadUInt64(); // Ordered by hash
                ulong unk = bs.ReadUInt64();
            }
        }
        else if (sec.Magic == 0x214B4E42) // BNK!
        {
            Console.WriteLine("Entering BNK! section");

            var bank = new Bank();
            bank.Read(bs);
            Banks.Add(bank);
        }
        else
        {
            return true;
        }

        return true;
    }

    public void ListFiles(string outputPath)
    {
        using var sw = new StreamWriter(outputPath);

        foreach (var bank in Banks)
        {
            string bigFileDataPath = Path.ChangeExtension(_bigDataFileName, bank.UsesSideBigFileMaybe == 1 ? $"b{bank.BigFileIndex:D2}" : "bfd");
            string bigFileName = Path.GetFileName(bigFileDataPath);

            sw.WriteLine($"# Bank: {bigFileName} ({bank.FileInfos.Count} files)");
            foreach (var info in bank.FileInfos.Values)
            {
                if (HashToPath.TryGetValue(info.Hash, out string? value))
                    sw.WriteLine($"{value} - hash: {info.Hash:X16}, flags:{info.Flags}, offset: 0x{info.Offset:X}, size: 0x{info.Size:X}, zsize: 0x{info.CompressedSize:X}");
                else
                    sw.WriteLine($"{info.Hash:X16} - flags:{info.Flags}, offset: 0x{info.Offset:X}, size: 0x{info.Size:X}, zsize: 0x{info.CompressedSize:X}");
            }

            sw.WriteLine();
        }
    }

    private int FindKey(Span<byte> input)
    {
        byte[] buf = new byte[8];
        for (int i = 0; i < Keys.XTEAKeyParameterTable.Count; i++)
        {
            XTEAParameter k = Keys.XTEAKeyParameterTable[i];
            XTEADecryptor.Decrypt(0x41455458, k, input, buf, 8);
            if (BinaryPrimitives.ReadInt32LittleEndian(buf) == 0x434F5443 || BinaryPrimitives.ReadInt32LittleEndian(buf) == 0x21434F54)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Decrypts data
    /// </summary>
    /// <param name="method"></param>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="length"></param>
    /// <param name="xteaParameter"></param>
    /// <exception cref="ArgumentException"></exception>
    private bool Decrypt(uint method, byte[] input, byte[] output, uint length, string xorKey, XTEAParameter xteaParameter)
    {
        if (method == 0x41455458) // XTEA
        {
            // Only 0x100 or 0x20000 is decrypted, the rest is plain text
            // This should be a callback rather than a function, meh
            return XTEADecryptor.Decrypt(method, xteaParameter, input, output, length);
        }
        else if (method == 0x2E534541) // "AES."
        {
            // Used in very old insider builds
            return AesDecrypt(method, input, output, length);
        }
        else if (method == 0x2E524F58) // "XOR."
        {
            return XorDecrypt(method, input, output, length, xorKey);
        }
        else
            throw new ArgumentException($"Unknown decryption method \"{Utils.MagicToString(method)}\"");
    }

    private static bool AesDecrypt(uint method, byte[] input, byte[] output, uint length)
    {
        if (method != 0x2E534541) // AES.
            return false;

        using Aes AES = Aes.Create();
        AES.Padding = PaddingMode.None;
        AES.Mode = CipherMode.CBC;
        AES.KeySize = 128;
        AES.BlockSize = 128;
        AES.IV = Encoding.ASCII.GetBytes(Keys.AES_IV);
        AES.Key = Encoding.ASCII.GetBytes(Keys.AES_KEY);

        using ICryptoTransform enc = AES.CreateDecryptor(AES.IV, AES.Key);
        enc.TransformBlock(input, 0, (int)(length &= 0xFFFFFFF0), output, 0);
        return true;
    }

    private static bool XorDecrypt(uint method, Span<byte> input, Span<byte> output, uint length, string xorKey)
    {
        if (method != 0x2E524F58) // XOR.
            return false;

        for (int i = 0; i < length; i++)
        {
            output[i] = (byte)(input[i] ^ xorKey[i]);
            if (i == xorKey.Length - 1)
                i = 0;
        }

        return true;
    }

    /// <summary>
    /// Decompresses data
    /// </summary>
    /// <param name="method"></param>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="decompressedSize"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private bool Decompress(uint method, byte[] input, byte[] output, uint decompressedSize)
    {
        if (method == 0x4E4B524B) // KRKN
            return Oodle.Decompress(input, output, decompressedSize);
        else
            throw new ArgumentException("Unknown decompression method");
    }


    public void Dispose()
    {
        foreach (var bank in Banks)
            bank.Dispose();
    }

    // Some utils
    static ulong HashPath(string path)
    {
        return Crc64.Compute(path.ToLower().Replace('/', '\\'));
    }
}
