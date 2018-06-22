using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    public enum LightType
    {
        Directional = 1,
        Ambient = 2,
        Point = 128,
        Spot = 129,

        Unknown = -1
    }

    [Flags]
    public enum LightFlags
    {
        LightAtomics = 1 << 0,
        LightWorld = 1 << 1
    }

    [Serializable]
    public class Light : ISceneSection
    {
        public UInt32 idx;
        public LightType type;
        public FColor color;
        public LightFlags flags;
        public Vector pos, vec; //vec is either dir or a lookAt point
        public float radius;

        public void read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            idx = reader.ReadUInt32();
            type = EnumUtils.intToEnum<LightType>(reader.ReadInt32());
            color = FColor.read(reader);
            flags = EnumUtils.intToFlags<LightFlags>(reader.ReadUInt32());
            pos = new Vector();
            vec = new Vector();
            radius = 0.0f;
            switch (type)
            {
                case LightType.Directional:
                    pos = Vector.read(reader);
                    vec = Vector.read(reader);
                    break;
                case LightType.Point:
                    radius = reader.ReadSingle();
                    pos = Vector.read(reader);
                    break;
                case LightType.Spot:
                    radius = reader.ReadUInt32();
                    pos = Vector.read(reader);
                    vec = Vector.read(reader);
                    break;
            }
        }

        public void write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(idx);
            writer.Write((int)type);
            color.write(writer);
            writer.Write((uint)flags);
            switch (type)
            {
                case LightType.Directional:
                    pos.write(writer);
                    vec.write(writer);
                    break;
                case LightType.Point:
                    writer.Write(radius);
                    pos.write(writer);
                    break;
                case LightType.Spot:
                    writer.Write(radius);
                    pos.write(writer);
                    vec.write(writer);
                    break;
            }
        }
    }
}