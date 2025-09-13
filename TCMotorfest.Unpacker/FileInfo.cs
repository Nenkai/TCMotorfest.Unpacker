using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCMotorfest.Unpacker
{
    public class FileInfo
    {
        public ulong NameHash { get; set; }
        public ulong Offset { get; set; }
        public uint CompressedSize { get; set; }
        public uint Size { get; set; }
        public FileFlags Flags { get; set; }
    }

    [Flags]
    public enum FileFlags
    {
        Compressed = 1 << 0,
        Encrypted = 1 << 1,
    }
}
