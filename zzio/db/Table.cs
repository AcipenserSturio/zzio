using System;
using System.IO;
using System.Collections.Generic;

namespace zzio.db;

[Serializable]
public class Table
{
    public Dictionary<UID, Row> rows = [];

    public void Read(Stream stream)
    {
        rows.Clear();
        using BinaryReader reader = new(stream);
        uint rowCount = reader.ReadUInt32();
        for (uint i = 0; i < rowCount; i++)
        {
            Row row = new();
            row.Read(reader);
            if (!rows.ContainsKey(row.uid))
                rows.Add(row.uid, row);
            else
                Console.WriteLine($"Malformed DB: {row.uid} already exists");
        }
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(rows.Count);
        foreach (Row row in rows.Values)
            row.Write(writer);
    }
}
