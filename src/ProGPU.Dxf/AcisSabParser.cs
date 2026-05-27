using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace ProGPU.Dxf;

public class AcisSabParser
{
    public static List<Acis3dEdge> ParseSab(byte[] sabBytes)
    {
        using var ms = new MemoryStream(sabBytes);
        return ParseSab(ms);
    }

    public static List<Acis3dEdge> ParseSab(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        byte[] bytes = ms.ToArray();

        var entities = ParseEntities(bytes);
        return ReconstructEdges(entities);
    }

    private static List<AcisEntity> ParseEntities(byte[] bytes)
    {
        var entities = new List<AcisEntity>();
        int length = bytes.Length;
        
        // Step 1: Scan the byte array to find all entity types
        // A length-prefixed string can have:
        // - 1-byte length prefix (preceded optionally by 0x03 string tag)
        // - 4-byte length prefix (preceded optionally by 0x04 string tag)
        var offsets = new List<(int start, int dataStart, string type)>();
        
        for (int i = 0; i < length; i++)
        {
            // Case A: 1-byte length prefix
            if (i + 1 < length)
            {
                int len1 = bytes[i];
                if (len1 >= 4 && len1 <= 20 && i + 1 + len1 <= length)
                {
                    string candidate = Encoding.ASCII.GetString(bytes, i + 1, len1);
                    if (IsEntityType(candidate))
                    {
                        // Check if it's preceded by 0x03 string tag
                        int startIdx = (i > 0 && bytes[i - 1] == 0x03) ? i - 1 : i;
                        offsets.Add((startIdx, i + 1 + len1, candidate.ToLowerInvariant()));
                        i += len1; // skip string bytes
                        continue;
                    }
                }
            }
            
            // Case B: 4-byte length prefix
            if (i + 4 < length)
            {
                int len4 = BitConverter.ToInt32(bytes, i);
                if (len4 >= 4 && len4 <= 20 && i + 4 + len4 <= length)
                {
                    string candidate = Encoding.ASCII.GetString(bytes, i + 4, len4);
                    if (IsEntityType(candidate))
                    {
                        // Check if it's preceded by 0x04 string tag
                        int startIdx = (i > 0 && bytes[i - 1] == 0x04) ? i - 1 : i;
                        offsets.Add((startIdx, i + 4 + len4, candidate.ToLowerInvariant()));
                        i += 3 + len4; // skip length and string bytes
                        continue;
                    }
                }
            }
        }

        // Step 2: Extract data fields for each entity within its byte range
        // We'll first try parsing with tags.
        bool hasAnyTags = false;
        var tempEntities = new List<AcisEntity>();
        
        for (int index = 0; index < offsets.Count; index++)
        {
            var current = offsets[index];
            int dataStart = current.dataStart;
            int dataEnd = (index + 1 < offsets.Count) ? offsets[index + 1].start : length;
            
            var entity = new AcisEntity
            {
                Index = index,
                Type = current.type,
                RawLine = current.type
            };
            
            int pos = dataStart;
            while (pos < dataEnd)
            {
                if (pos + 1 > dataEnd) break;
                byte b = bytes[pos];
                
                if (b == 0x01) // Integer
                {
                    if (pos + 5 <= dataEnd)
                    {
                        int intVal = BitConverter.ToInt32(bytes, pos + 1);
                        entity.Values.Add(intVal);
                        hasAnyTags = true;
                        pos += 5;
                    }
                    else
                    {
                        pos++;
                    }
                }
                else if (b == 0x02) // Double
                {
                    if (pos + 9 <= dataEnd)
                    {
                        double doubleVal = BitConverter.ToDouble(bytes, pos + 1);
                        entity.Values.Add(doubleVal);
                        hasAnyTags = true;
                        pos += 9;
                    }
                    else
                    {
                        pos++;
                    }
                }
                else if (b == 0x0C) // Pointer
                {
                    if (pos + 5 <= dataEnd)
                    {
                        int ptrVal = BitConverter.ToInt32(bytes, pos + 1);
                        entity.Pointers.Add(ptrVal);
                        hasAnyTags = true;
                        pos += 5;
                    }
                    else
                    {
                        pos++;
                    }
                }
                else if (b == 0x03) // Short string
                {
                    if (pos + 2 <= dataEnd)
                    {
                        int strLen = bytes[pos + 1];
                        pos += 2 + strLen;
                    }
                    else
                    {
                        pos++;
                    }
                }
                else if (b == 0x04) // Long string
                {
                    if (pos + 5 <= dataEnd)
                    {
                        int strLen = BitConverter.ToInt32(bytes, pos + 1);
                        pos += 5 + Math.Max(0, strLen);
                    }
                    else
                    {
                        pos++;
                    }
                }
                else
                {
                    pos++;
                }
            }
            
            tempEntities.Add(entity);
        }
        
        // If we found any tags, we return the tagged parsing result.
        // Otherwise, we perform tagless parsing.
        if (hasAnyTags)
        {
            entities.AddRange(tempEntities);
        }
        else
        {
            for (int index = 0; index < offsets.Count; index++)
            {
                var current = offsets[index];
                int dataStart = current.dataStart;
                int dataEnd = (index + 1 < offsets.Count) ? offsets[index + 1].start : length;
                
                var entity = new AcisEntity
                {
                    Index = index,
                    Type = current.type,
                    RawLine = current.type
                };
                
                var (pointerCount, doubleCount) = GetEntityFieldCounts(current.type);
                int pos = dataStart;
                
                // Read pointers
                for (int p = 0; p < pointerCount && pos + 4 <= dataEnd; p++)
                {
                    int ptrVal = BitConverter.ToInt32(bytes, pos);
                    entity.Pointers.Add(ptrVal);
                    pos += 4;
                }
                
                // Read doubles
                for (int d = 0; d < doubleCount && pos + 8 <= dataEnd; d++)
                {
                    double doubleVal = BitConverter.ToDouble(bytes, pos);
                    entity.Values.Add(doubleVal);
                    pos += 8;
                }
                
                entities.Add(entity);
            }
        }

        return entities;
    }

    private static bool IsEntityType(string type)
    {
        string lower = type.ToLowerInvariant();
        return lower == "body" || lower == "lump" || lower == "shell" || lower == "face" || 
               lower == "loop" || lower == "coedge" || lower == "edge" || lower == "vertex" || 
               lower == "point" || lower == "straight" || lower == "ellipse" || lower == "intcurve";
    }

    private static (int pointerCount, int doubleCount) GetEntityFieldCounts(string type)
    {
        switch (type)
        {
            case "body":
            case "lump":
            case "shell":
            case "face":
            case "loop":
            case "coedge":
                return (4, 0);
            case "edge":
                return (5, 0);
            case "vertex":
                return (2, 0);
            case "point":
                return (1, 3);
            case "straight":
                return (1, 0);
            case "ellipse":
                return (1, 6);
            case "intcurve":
                return (1, 0);
            default:
                return (0, 0);
        }
    }

    private static List<Acis3dEdge> ReconstructEdges(List<AcisEntity> entities)
    {
        var edges = new List<Acis3dEdge>();

        foreach (var ent in entities)
        {
            if (ent.Type == "edge")
            {
                // In an edge record, pointers are:
                // Pointers[0]: coedge1
                // Pointers[1]: coedge2
                // Pointers[2]: start vertex
                // Pointers[3]: end vertex
                // Pointers[4]: curve geometry
                if (ent.Pointers.Count >= 4)
                {
                    int v1Idx = ent.Pointers[2];
                    int v2Idx = ent.Pointers[3];

                    if (v1Idx >= 0 && v1Idx < entities.Count && v2Idx >= 0 && v2Idx < entities.Count)
                    {
                        var v1 = entities[v1Idx];
                        var v2 = entities[v2Idx];

                        // In a vertex record, the second pointer is the point geometry
                        if (v1.Type == "vertex" && v1.Pointers.Count >= 2 &&
                            v2.Type == "vertex" && v2.Pointers.Count >= 2)
                        {
                            int p1Idx = v1.Pointers[1];
                            int p2Idx = v2.Pointers[1];

                            if (p1Idx >= 0 && p1Idx < entities.Count && p2Idx >= 0 && p2Idx < entities.Count)
                            {
                                var pt1 = entities[p1Idx];
                                var pt2 = entities[p2Idx];

                                // In a point record, the double values represent the coordinates (X, Y, Z)
                                if (pt1.Type == "point" && pt1.Values.Count >= 3 &&
                                    pt2.Type == "point" && pt2.Values.Count >= 3)
                                {
                                    var startPt = new Vector3((float)pt1.Values[0], (float)pt1.Values[1], (float)pt1.Values[2]);
                                    var endPt = new Vector3((float)pt2.Values[0], (float)pt2.Values[1], (float)pt2.Values[2]);

                                    string curveType = "straight";
                                    if (ent.Pointers.Count >= 5)
                                    {
                                        int curveIdx = ent.Pointers[4];
                                        if (curveIdx >= 0 && curveIdx < entities.Count)
                                        {
                                            curveType = entities[curveIdx].Type;
                                        }
                                    }

                                    edges.Add(new Acis3dEdge
                                    {
                                        StartPoint = startPt,
                                        EndPoint = endPt,
                                        CurveType = curveType
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        return edges;
    }
}
