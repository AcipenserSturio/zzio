using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;

namespace zzio.rwbs
{
    [Serializable]
    public class RWGeometryList : StructSection
    {
        public override SectionId sectionId { get { return SectionId.GeometryList; } }

        public UInt32 geometryCount;

        protected override void readStruct(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            geometryCount = reader.ReadUInt32();
        }

        protected override void writeStruct(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(geometryCount);
        }
    }
}