using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;

namespace TCMotorfest.Unpacker;

public class Bank : IDisposable
{
    public uint BigFileIndex { get; set; }
    public uint UsesSideBigFileMaybe { get; set; }
    public Dictionary<ulong, FileInfo> FileInfos { get; set; } = new();

    public FileStream Stream { get; private set; }

    public bool Initialized { get; private set; } = false;

    public void Read(BinaryStream bs)
    {
        BigFileIndex = bs.ReadUInt32();
        UsesSideBigFileMaybe = bs.ReadUInt32();
        uint fileCount = bs.ReadUInt32();

        for (int i = 0; i < fileCount; i++)
        {
            var fileInfo = new FileInfo();
            fileInfo.Hash = bs.ReadUInt64();
            fileInfo.Offset = bs.ReadUInt64();
            fileInfo.CompressedSize = bs.ReadUInt32();
            fileInfo.Size = bs.ReadUInt32();
            fileInfo.Flags = (FileFlags)bs.ReadInt32();
            FileInfos.Add(fileInfo.Hash, fileInfo);
        }
    }

    public void InitStream(string fileName)
    {
        Stream = new FileStream(fileName, FileMode.Open);
        Initialized = true;
    }


    public void Dispose()
    {
        Stream?.Dispose();
    }
}
