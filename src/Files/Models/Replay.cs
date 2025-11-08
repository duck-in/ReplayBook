namespace Fraxiinus.ReplayBook.Files.Models;

using Fraxiinus.ReplayBook.Files.Utilities;
using Fraxiinus.Rofl.Extract.Data;
using Fraxiinus.Rofl.Extract.Data.Models;
using Fraxiinus.Rofl.Extract.Data.Models.Rofl2;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class Replay
{
    public Replay() { }

    public Replay(string id, ParseResult input)
    {
        Name = Path.GetFileName(id);
        Id = id;

        Type = input.Type;
        switch (Type)
        {
            case ReplayType.ROFL2:
                LoadFromROFL2File((ROFL2)input.Result);
                break;
            case ReplayType.ROFL:
                LoadFromROFLFile((ROFL)input.Result);
                break;
            case ReplayType.Unknown:
                throw new Exception("Unknown replay type");
        }

        // Infer values
        MapName = GameDetailsInferrer.GetMapName(GameDetailsInferrer.InferMap(Players));
        IsBlueVictorious = GameDetailsInferrer.InferBlueVictory(BluePlayers, RedPlayers);
    }

    private void LoadFromROFL2File(ROFL2 input)
    {
        // Copy values
        GameDuration = TimeSpan.FromMilliseconds(input.Metadata.GameLength);
        GameVersion = input.Metadata.GameVersion;
        MatchId = "Unknown";

        // UniqueId must be unique for every player in a match.
        // It is used to optimize the player cache so the same object isn't loaded twice

        BluePlayers = input.Metadata.PlayerStatistics
            .Where(x => x.Team == "100")
            .Select(y =>
            {
                y.UniqueId = $"{y.Id}_{y.Exp}_{GameDuration}";
                return y;
            }).ToList();
        RedPlayers = input.Metadata.PlayerStatistics
            .Where(x => x.Team == "200")
            .Select(y =>
            {
                y.UniqueId = $"{y.Id}_{y.Exp}_{GameDuration}";
                return y;
            }).ToList();
    }

    private void LoadFromROFLFile(ROFL input)
    {
        // Copy values
        GameDuration = TimeSpan.FromMilliseconds(input.Metadata.GameLength);
        GameVersion = input.Metadata.GameVersion;
        MatchId = input.PayloadHeader.GameId.ToString();

        BluePlayers = input.Metadata.PlayerStatistics
            .Where(x => x.Team == "100")
            .Select(y =>
            {
                y.UniqueId = $"{y.Id}_{y.Exp}_{GameDuration}";
                return RoflBaseClassConverter.ToPlayerStats2(y);
            }).ToList();
        RedPlayers = input.Metadata.PlayerStatistics
            .Where(x => x.Team == "200")
            .Select(y =>
            {
                y.UniqueId = $"{y.Id}_{y.Exp}_{GameDuration}";
                return RoflBaseClassConverter.ToPlayerStats2(y);
            }).ToList();
    }

    /// <summary>
    /// Name of the file
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Full path of the file
    /// </summary>
    [BsonId]
    public string Id { get; set; }

    public TimeSpan GameDuration { get; set; }

    public string GameVersion { get; set; }

    public string MatchId { get; set; }

    [BsonIgnore]
    public IEnumerable<PlayerStats2> Players => BluePlayers.Union(RedPlayers);

    [BsonRef("players")]
    public List<PlayerStats2> BluePlayers { get; set; }

    [BsonRef("players")]
    public List<PlayerStats2> RedPlayers { get; set; }

    private ReplayType Type { get; }

    public string MapName { get; set; }

    public bool IsBlueVictorious { get; set; }

    public override string ToString()
    {
        return ToString("");
    }
    
    public string ToString(string prefix)
    {
        return
            $"{prefix}Replay {{\r\n" +
            $"{prefix}\t{nameof(Name)}: {Name},\r\n" +
            $"{prefix}\t{nameof(Id)}: {Id ?? "[null]"},\r\n" +
            $"{prefix}\t{nameof(GameDuration)}: {GameDuration},\r\n" +
            $"{prefix}\t{nameof(GameVersion)}: {GameVersion},\r\n" +
            $"{prefix}\t{nameof(MatchId)}: {MatchId},\r\n" +
            $"{prefix}\t{nameof(BluePlayers)}: {(BluePlayers == null ? "null" : "[BluePlayers]")},\r\n" +
            $"{prefix}\t{nameof(RedPlayers)}: {(RedPlayers == null ? "null" : "[RedPlayers]")},\r\n" +
            $"{prefix}\t{nameof(Type)}: {Type},\r\n" +
            $"{prefix}\t{nameof(MapName)}: {MapName},\r\n" +
            $"{prefix}\t{nameof(IsBlueVictorious)}: {IsBlueVictorious}\r\n" +
            $"}}";
    }
}
