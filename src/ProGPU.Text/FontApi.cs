using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProGPU.Text;

public class FontInfo
{
    public string Name { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{Name} ({FamilyName})";
    }
}

public static class FontApi
{
    public static List<FontInfo> GetSystemFonts()
    {
        var list = new List<FontInfo>();
        var paths = new List<string>();

        if (OperatingSystem.IsMacOS())
        {
            paths.Add("/System/Library/Fonts");
            paths.Add("/System/Library/Fonts/Supplemental");
            paths.Add("/Library/Fonts");
        }
        else if (OperatingSystem.IsWindows())
        {
            paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts"));
        }
        else if (OperatingSystem.IsLinux())
        {
            paths.Add("/usr/share/fonts");
            paths.Add("/usr/local/share/fonts");
            paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts"));
        }

        foreach (var dir in paths)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".ttf" || ext == ".ttc" || ext == ".otf")
                    {
                        var info = ParseFontInfo(file);
                        if (info != null)
                        {
                            list.Add(info);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FontApi] Error scanning directory {dir}: {ex.Message}");
            }
        }

        // Deduplicate and sort
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueList = new List<FontInfo>();
        foreach (var font in list)
        {
            if (!seen.Contains(font.Name))
            {
                seen.Add(font.Name);
                uniqueList.Add(font);
            }
        }

        uniqueList.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return uniqueList;
    }

    public static FontInfo? ParseFontInfo(string file)
    {
        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(fs);

            if (fs.Length < 12) return null;
            uint sfntVersion = ReadUIntBE(reader);
            
            uint baseOffset = 0;
            if (sfntVersion == 0x74746366) // "ttcf" (TTC collection)
            {
                if (fs.Length < 16) return null;
                reader.BaseStream.Seek(12, SeekOrigin.Begin);
                baseOffset = ReadUIntBE(reader);
                reader.BaseStream.Seek(baseOffset, SeekOrigin.Begin);
                sfntVersion = ReadUIntBE(reader);
            }

            ushort numTables = ReadUShortBE(reader);
            reader.BaseStream.Seek(baseOffset + 12, SeekOrigin.Begin);

            uint nameTableOffset = 0;
            uint nameTableLength = 0;

            for (int i = 0; i < numTables; i++)
            {
                uint tag = ReadUIntBE(reader);
                uint checksum = ReadUIntBE(reader);
                uint offset = ReadUIntBE(reader);
                uint length = ReadUIntBE(reader);

                if (tag == 0x6E616D65) // "name" tag
                {
                    nameTableOffset = offset;
                    nameTableLength = length;
                    break;
                }
            }

            if (nameTableOffset == 0)
            {
                var fn = Path.GetFileNameWithoutExtension(file);
                return new FontInfo { Name = fn, FamilyName = fn, FilePath = file };
            }

            reader.BaseStream.Seek(nameTableOffset, SeekOrigin.Begin);
            ushort format = ReadUShortBE(reader);
            ushort count = ReadUShortBE(reader);
            ushort stringOffset = ReadUShortBE(reader);

            string familyName = string.Empty;
            string fullName = string.Empty;

            for (int i = 0; i < count; i++)
            {
                ushort platformId = ReadUShortBE(reader);
                ushort encodingId = ReadUShortBE(reader);
                ushort languageId = ReadUShortBE(reader);
                ushort nameId = ReadUShortBE(reader);
                ushort length = ReadUShortBE(reader);
                ushort offset = ReadUShortBE(reader);

                if (nameId == 1 || nameId == 4)
                {
                    long pos = reader.BaseStream.Position;
                    reader.BaseStream.Seek(nameTableOffset + stringOffset + offset, SeekOrigin.Begin);
                    byte[] bytes = reader.ReadBytes(length);
                    reader.BaseStream.Seek(pos, SeekOrigin.Begin);

                    string nameVal = string.Empty;
                    // Standard Windows (Platform 3) encoding is UTF-16BE
                    if (platformId == 3 || platformId == 0)
                    {
                        nameVal = Encoding.BigEndianUnicode.GetString(bytes);
                    }
                    else
                    {
                        nameVal = Encoding.UTF8.GetString(bytes);
                    }

                    // Strip null characters or garbage
                    nameVal = nameVal.Replace("\0", "").Trim();

                    if (nameId == 1 && string.IsNullOrEmpty(familyName)) familyName = nameVal;
                    if (nameId == 4 && string.IsNullOrEmpty(fullName)) fullName = nameVal;
                }
            }

            if (string.IsNullOrEmpty(familyName)) familyName = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(fullName)) fullName = familyName;

            return new FontInfo
            {
                Name = fullName,
                FamilyName = familyName,
                FilePath = file
            };
        }
        catch
        {
            try
            {
                var fn = Path.GetFileNameWithoutExtension(file);
                return new FontInfo { Name = fn, FamilyName = fn, FilePath = file };
            }
            catch
            {
                return null;
            }
        }
    }

    private static ushort ReadUShortBE(BinaryReader reader)
    {
        byte[] b = reader.ReadBytes(2);
        return (ushort)((b[0] << 8) | b[1]);
    }

    private static uint ReadUIntBE(BinaryReader reader)
    {
        byte[] b = reader.ReadBytes(4);
        return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    }
}
