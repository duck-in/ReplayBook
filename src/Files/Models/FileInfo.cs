using System;
using System.IO;
using LiteDB;

namespace Fraxiinus.ReplayBook.Files.Models;

public class FileInfo
{
    public FileInfo() {}

    public FileInfo(System.IO.FileInfo fileInfo)
    {
        Name = System.IO.Path.GetFileNameWithoutExtension(fileInfo.FullName);
        Path = fileInfo.FullName;
        CreationTime = fileInfo.CreationTime;
        Size =  fileInfo.Length;
    }
    
    public string Name { get; set; }

    [BsonId]
    public string Path { get; set; }

    public DateTime CreationTime { get; set; }

    public virtual long Size { get; set; }
    
    public override string ToString()
    {
        return ToString("");
    }

    public virtual string ToString(string prefix)
    {
        return
            $"{prefix}FileInfo {{\r\n" +
            $"{prefix}\t{nameof(Name)}: {Name},\r\n" +
            $"{prefix}\t{nameof(Path)}: {Path ?? "[null]"}\r\n" +
            $"{prefix}}}";
    }
}