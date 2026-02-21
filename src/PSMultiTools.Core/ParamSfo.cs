using System.Text;

namespace PSMultiTools.Core;

public class ParamSfo
{
    public string? Title { get; set; }
    public string? TitleId { get; set; }
    public string? Version { get; set; }
    public string? AppVersion { get; set; }
    public string? Category { get; set; }
    public string? ContentId { get; set; }

    public static ParamSfo? Parse(byte[] data)
    {
        if (data.Length < 0x14) return null;

        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        var magic = reader.ReadUInt32();   // 0x46535000 ("\0PSF")
        var version = reader.ReadUInt32(); // 0x00000101
        var keyTableStart = reader.ReadUInt32();
        var dataTableStart = reader.ReadUInt32();
        var entriesCount = reader.ReadUInt32();

        if (magic != 0x46535000) return null;

        var result = new ParamSfo();
        var entries = new List<SfoEntry>();

        for (int i = 0; i < entriesCount; i++)
        {
            var keyOffset = reader.ReadUInt16();
            var format = reader.ReadUInt16(); // 0x0204 = UTF8 null-terminated, 0x0004 = UTF8 null-terminated
            var len = reader.ReadUInt32();
            var maxLen = reader.ReadUInt32();
            var dataOffset = reader.ReadUInt32();

            entries.Add(new SfoEntry(keyOffset, format, len, maxLen, dataOffset));
        }

        foreach (var entry in entries)
        {
            try
            {
                ms.Position = keyTableStart + entry.KeyOffset;
                var key = ReadNullTerminatedString(reader);

                ms.Position = dataTableStart + entry.DataOffset;
                // Only handle string types (0x0004, 0x0204)
                if (entry.Format == 0x0004 || entry.Format == 0x0204)
                {
                    var valueBytes = reader.ReadBytes((int)entry.Length);
                    // Remove null terminator if present
                    var value = Encoding.UTF8.GetString(valueBytes).TrimEnd('\0');

                    switch (key)
                    {
                        case "TITLE": result.Title = value; break;
                        case "TITLE_ID": result.TitleId = value; break;
                        case "VERSION": result.Version = value; break;
                        case "APP_VER": result.AppVersion = value; break;
                        case "CATEGORY": result.Category = value; break;
                        case "CONTENT_ID": result.ContentId = value; break;
                    }
                }
            }
            catch { }
        }

        return result;
    }

    private static string ReadNullTerminatedString(BinaryReader reader)
    {
        var bytes = new List<byte>();
        while (true)
        {
            var b = reader.ReadByte();
            if (b == 0) break;
            bytes.Add(b);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private record SfoEntry(ushort KeyOffset, ushort Format, uint Length, uint MaxLen, uint DataOffset);
}
