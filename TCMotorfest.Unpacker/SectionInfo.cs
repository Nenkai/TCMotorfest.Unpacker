using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCMotorfest.Unpacker
{
    public class SectionInfo
    {
        public uint Magic { get; set; }
        public uint Version { get; set; }
        public long SectionDataOffset { get; set; }
        public uint SectionDataSize { get; set; }
        public uint SectionCount { get; set; }

        public List<SectionInfo> Child { get; set; } = new();

        public override string ToString()
        {
            return $"{Magic:X8} - Child: {Child.Count}";
        }
    }
}
