using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;

namespace Fraxiinus.ReplayBook.Files.Models;

public class FolderInfo : FileInfo
{

    public FolderInfo() { }

    public FolderInfo(DirectoryInfo dir, int depthLevel)
    {
        Name = dir.Name;
        Path = dir.FullName;
        CreationTime = dir.CreationTime;
        Files = new List<FileInfo>();
        DepthLevel = depthLevel;
        
        var files = dir.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            switch (file)
            {
                case DirectoryInfo subfolder:
                    Files.Add(new FolderInfo(subfolder, depthLevel + 1));
                    break;
                case System.IO.FileInfo innerFile when file.Name.EndsWith(".rofl", StringComparison.OrdinalIgnoreCase):
                    Files.Add(new FileInfo(innerFile));
                    break;
            }
        }
    }
    
    public int DepthLevel { get; set; }
    
    [BsonRef("fileInfo")]
    public List<FileInfo> Files { get; set; }
    
    [BsonIgnore]
    public List<ReplayFile> ReplayFiles { get; set; }
    
    [BsonIgnore]
    public override long Size => Files.Select(f => f.Size).Sum();

    public override string ToString()
    {
        return ToString("");
    }

    public override string ToString(string prefix)
    {
        var filesString = Files == null || Files.Count == 0
            ? "[]"
            : $"[\r\n" +
              $"{string.Join(",\r\n", Files.Select(f => f.ToString($"{prefix}\t\t")))}\r\n" +
              $"{prefix}\t]";

        var replayFilesString = ReplayFiles == null || ReplayFiles.Count == 0
            ? "[]"
            : $"[\r\n" +
              $"{string.Join($",\r\n", ReplayFiles.Select(f => f.ToString($"{prefix}\t\t")))}\r\n" +
              $"{prefix}\t]";
        
        return $"{prefix}FolderInfo {{\r\n" +
               $"{prefix}\tName: {Name}\r\n" +
               $"{prefix}\tPath: {Path ?? "[null]"}\r\n" +
               $"{prefix}\tFiles: {filesString}\r\n" +
               $"{prefix}\tReplayFiles: {replayFilesString}\r\n" +
               $"{prefix}}}";
    }
}