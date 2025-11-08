using LiteDB;

namespace Fraxiinus.ReplayBook.Files.Models;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public class ReplayFile
{
    // Constructor for LiteDB
    public ReplayFile() { }

    // Initial constructor for first time file is parsed
    public ReplayFile(FileInfo fileInfo, Replay replay)
    {
        FileInfo = fileInfo ?? throw new ArgumentNullException(nameof(fileInfo));
        Replay = replay ?? throw new ArgumentNullException(nameof(replay));

        Id = FileInfo.Path;
        AlternativeName = FileInfo.Name;

        SearchKeywords = new List<string>
        {
            FileInfo.Name.ToUpper(CultureInfo.InvariantCulture)
        };
        SearchKeywords.AddRange(Replay.Players.Select(x => x.Name.ToUpper(CultureInfo.InvariantCulture)));
        SearchKeywords.AddRange(Replay.Players.Select(x => x.Skin.ToUpper(CultureInfo.InvariantCulture)));
    }

    // Error constructor
    public ReplayFile(FileInfo fileInfo, ReplayErrorInfo errorInfo)
    {
        FileInfo = fileInfo ?? throw new ArgumentNullException(nameof(fileInfo));
        ErrorInfo = errorInfo ?? throw new ArgumentNullException(nameof(errorInfo));

        Id = FileInfo.Path;
        AlternativeName = FileInfo.Name;

        SearchKeywords = new List<string>
        {
            FileInfo.Name.ToUpper(CultureInfo.InvariantCulture),
        };
    }

    [BsonRef("fileInfo")]
    public FileInfo FileInfo { get; set; }

    /// <summary>
    /// Will be null if replay failed to parse
    /// </summary>
    [BsonRef("replay")]
    public Replay Replay { get; set; }

    /// <summary>
    /// Will be set if replay failed to parse
    /// </summary>
    [BsonRef("errorInfo")]
    public ReplayErrorInfo ErrorInfo { get; set; }

    /// <summary>
    /// Full file path used as ID
    /// </summary>
    [BsonId]
    public string Id { get; set; }

    // The following fields are used to allow for fast indexing
    // Placing them on the root level object makes creating indexes very easy and clear.
    public List<string> SearchKeywords { get; set; }

    // User Assigned Field
    public string AlternativeName { get; set; }

    public override string ToString()
    {
        return ToString("");
    }
    
    public string ToString(string prefix)
    {
        return
            $"{prefix}ReplayFile {{\r\n" +
            $"{prefix}\t{nameof(FileInfo)}: {FileInfo.ToString("\t")},\r\n" +
            $"{prefix}\t{nameof(Replay)}: {Replay.ToString("\t")},\r\n" +
            $"{prefix}\t{nameof(Id)}: {Id},\r\n" +
            $"{prefix}\t{nameof(SearchKeywords)}: [{string.Join(", ", SearchKeywords)}],\r\n" +
            $"{prefix}\t{nameof(AlternativeName)}: {AlternativeName}\r\n" +
            $"{prefix}}}";
    }
}
