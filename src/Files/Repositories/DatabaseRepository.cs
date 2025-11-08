namespace Fraxiinus.ReplayBook.Files.Repositories;

using Etirps.RiZhi;
using Fraxiinus.ReplayBook.Configuration.Models;
using Fraxiinus.ReplayBook.Files.Models;
using Fraxiinus.ReplayBook.Files.Models.Search;
using Fraxiinus.Rofl.Extract.Data.Models.Rofl2;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

public class DatabaseRepository
{
    private readonly RiZhi _log;
    private readonly string _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "replayCache.db");
    private readonly ObservableConfiguration _config;

    public DatabaseRepository(ObservableConfiguration config, RiZhi log)
    {
        _config = config;
        _log = log;

        try
        {
            InitializeDatabase();
        }
        catch (Exception ex)
        {
            _log.Warning($"Database file is invalid, deleting and trying again... exception:{ex}");
            File.Delete(_filePath);
            InitializeDatabase();
        }
    }

    public string GetDatabasePath()
    {
        return _filePath;
    }

    public void DeleteDatabase()
    {
        File.Delete(_filePath);
    }

    private void InitializeDatabase()
    {
        if (!Directory.Exists(Path.GetDirectoryName(_filePath)))
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
        }

        using var db = new LiteDatabase(_filePath);
        // Prevent empty string values from being lost!
        BsonMapper.Global.EmptyStringToNull = false;

        // Create and verify file results collection
        ILiteCollection<ReplayFile> replayFiles = db.GetCollection<ReplayFile>("replayFile");
        
        _ = BsonMapper.Global.Entity<PlayerStats2>()
            .Id(r => r.UniqueId);
        
        _ = replayFiles.EnsureIndex(x => x.FileInfo.Name);
        _ = replayFiles.EnsureIndex(x => x.AlternativeName);
        _ = replayFiles.EnsureIndex(x => x.FileInfo.Size);
        _ = replayFiles.EnsureIndex(x => x.FileInfo.CreationTime);
        _ = replayFiles.EnsureIndex(x => x.SearchKeywords);
    }

    public void AddReplayFile(ReplayFile replayFile)
    {
        if (replayFile == null) { throw new ArgumentNullException(nameof(replayFile)); }
        if (replayFile.FileInfo == null) { throw new ArgumentNullException(nameof(replayFile)); }

        using var db = new LiteDatabase(_filePath);

        var replayFiles = db.GetCollection<ReplayFile>("replayFile");
        var replays = db.GetCollection<Replay>("replay");
        var fileInfos = db.GetCollection<Models.FileInfo>("fileInfo");
        var replayErrors = db.GetCollection<ReplayErrorInfo>("replayErrorInfo");
        var players = db.GetCollection<PlayerStats2>("players");

        // If we already have the file, do nothing
        if (replayFiles.FindById(replayFile.Id) != null) return;
        
        replayFiles.Insert(replayFile);
        fileInfos.Insert(replayFile.FileInfo);
        
        if (replayFile.Replay != null)
        {
            replays.Insert(replayFile.Replay);

            foreach (var player in replayFile.Replay.Players)
            {
                // If the player already exists, do nothing
                if (players.FindById(player.UniqueId) == null)
                {
                    players.Insert(player);
                }
            }
        }
        else if (replayFile.ErrorInfo != null)
        {
            replayErrors.Insert(replayFile.ErrorInfo);
        }
    }

    public void AddFile(Models.FileInfo fileInfo)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);

        using var db = new LiteDatabase(_filePath);

        var fileInfos = db.GetCollection<Models.FileInfo>("fileInfo");

        // If we already have the file, do nothing
        if (fileInfos.FindById(fileInfo.Path) != null) return;
        
        fileInfos.Insert(fileInfo);
    }

    public void RemoveReplayFile(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(id);
        }

        using var db = new LiteDatabase(_filePath);

        var replayFiles = db.GetCollection<ReplayFile>("replayFile");
        var fileInfos = db.GetCollection<Models.FileInfo>("fileInfo");
        var replays = db.GetCollection<Replay>("replay");
        var replayErrors = db.GetCollection<ReplayErrorInfo>("replayErrorInfo");
        var players = db.GetCollection<PlayerStats2>("players");

        ReplayFile replayFile = replayFiles
            .Include("$.Replay")
            .Include("$.Replay.BluePlayers[*]")
            .Include("$.Replay.RedPlayers[*]")
            .FindById(id);
        if (replayFile == null) return;

        if (replayFile.Replay != null)
        {
            foreach (PlayerStats2 player in replayFile.Replay.Players)
            {
                players.Delete(player.UniqueId);
            }
        }

        replayFiles.Delete(id);
        fileInfos.Delete(id);
        replays.Delete(id);
        replayErrors.Delete(id);
    }

    public void RemoveFolder(string path)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }

        using var db = new LiteDatabase(_filePath);
        
        RemoveFolderRecursive(db, path);
    }

    private void RemoveFolderRecursive(LiteDatabase db, string path)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        
        var fileInfos = db.GetCollection<Models.FileInfo>("fileInfo");
        
        if (fileInfos.FindById(path) is FolderInfo folderInfo)
        {
            folderInfo.Files = folderInfo.Files.Select(f => fileInfos.FindById(f.Path)).ToList();
            folderInfo.Files.OfType<FolderInfo>().ToList().ForEach(f => RemoveFolderRecursive(db, f.Path));
            
            var replayFiles = db.GetCollection<ReplayFile>("replayFile").Include(f => f.FileInfo);
            replayFiles.DeleteMany("REPLACE(REPLACE($.FileInfo.$id, '.rofl', ''), @0, '') = $.FileInfo.Name", folderInfo.Path + "\\");
        }
        
        fileInfos.Delete(path);
    }

    public void SetNewFolderId(string path, string newPath)
    {        
        if (path == null) { throw new ArgumentNullException(nameof(path)); }

        using var db = new LiteDatabase(_filePath);
        
        var fileInfos = db.GetCollection<Models.FileInfo>("fileInfo");
        
        var fileInfo = fileInfos.FindById(path);
        fileInfo.Path = newPath;
        fileInfos.Update(path, fileInfo);
    }
    
    public ReplayFile GetReplayFile(string id)
    {
        if (string.IsNullOrEmpty(id)) { throw new ArgumentNullException(id); }

        using var db = new LiteDatabase(_filePath);

        return db.GetCollection<ReplayFile>("replayFile")
            .Include("$.FileInfo")
            .Include("$.Replay")
            .Include("$.ErrorInfo")
            .Include("$.Replay.BluePlayers[*]")
            .Include("$.Replay.RedPlayers[*]")
            .FindById(id);
    }

    public IReadOnlyCollection<ReplayFile> GetReplayFiles(IEnumerable<string> ids)
    {
        using var db = new LiteDatabase(_filePath);

        return db.GetCollection<ReplayFile>("replayFile")
            .Include("$.FileInfo")
            .Include("$.Replay")
            .Include("$.ErrorInfo")
            .Include("$.Replay.BluePlayers[*]")
            .Include("$.Replay.RedPlayers[*]")
            .Find(r => ids.Contains(r.Id)).ToList();
    }

    public IEnumerable<ReplayFile> GetReplayFiles()
    {
        using var db = new LiteDatabase(_filePath);
        var replayFiles = db.GetCollection<ReplayFile>("replayFile")
            .Include("$.FileInfo")
            .Include("$.Replay")
            .Include("$.ErrorInfo")
            .Include("$.Replay.BluePlayers[*]")
            .Include("$.Replay.RedPlayers[*]");

        return replayFiles.FindAll().ToList();
    }

    public IEnumerable<FolderInfo> GetAllFolders()
    {
        using var db = new LiteDatabase(_filePath);
        var fileInfo = db.GetCollection<Models.FileInfo>("fileInfo").Include("$.Files");

        return fileInfo.FindAll().OfType<FolderInfo>().ToList();
    }
    
    public List<ReplayFile> QueryReplayFiles(string[] keywords, SortMethod sort, int maxEntries, int skip)
    {
        using var db = new LiteDatabase(_filePath);
        return QueryReplayFiles(db, keywords, sort).Limit(maxEntries).Offset(skip).ToList();
    }

    public IReadOnlyCollection<FolderInfo> QueryTopLevelFolders(string[] keywords, SortMethod sort, int maxEntries, int skip)
    {
        if (keywords == null) { throw new ArgumentNullException(nameof(keywords)); }

        using var db = new LiteDatabase(_filePath);

        // include all references
        var foldersQueryable = db.GetCollection<Models.FileInfo>("fileInfo").Query();

        // apply sort method to query
        foldersQueryable = sort switch
        {
            // sort by name depends on if we are using file names, or alternative names
            SortMethod.NameAsc => foldersQueryable.OrderBy(x => x.Name),
            SortMethod.NameDesc => foldersQueryable.OrderByDescending(x => x.Name),
            SortMethod.DateAsc => foldersQueryable.OrderBy(x => x.CreationTime),
            SortMethod.DateDesc => foldersQueryable.OrderByDescending(x => x.CreationTime),
            SortMethod.SizeAsc => foldersQueryable.OrderBy(x => x.Size),
            SortMethod.SizeDesc => foldersQueryable.OrderByDescending(x => x.Size),
            _ => foldersQueryable.OrderBy(x => x.CreationTime)
        };

        var result = foldersQueryable
            .Where("$.DepthLevel = 0").Limit(maxEntries).Offset(skip).ToList()
            .OfType<FolderInfo>().ToList();
        
        foreach (var f in result)
        {
            LoadHierarchy(db, f, keywords, sort);
        }

        return result;
    }

    private void LoadHierarchy(LiteDatabase db, FolderInfo folderInfo, string[] keywords, SortMethod sort)
    {
        var folders = db.GetCollection<Models.FileInfo>("fileInfo");
        
        folderInfo.Files = folderInfo.Files.Select(f => folders.FindById(f.Path)).ToList();
        folderInfo.Files.OfType<FolderInfo>().ToList().ForEach(f => LoadHierarchy(db, f, keywords, sort));
        
        folderInfo.ReplayFiles = QueryReplayFiles(db, keywords, sort)
            .Where("REPLACE(REPLACE($.FileInfo.$id, '.rofl', ''), @0, '') = $.FileInfo.Name", folderInfo.Path + "\\").ToList();
    }

    private ILiteQueryable<ReplayFile> QueryReplayFiles(LiteDatabase db, string[] keywords, SortMethod sort)
    {
        if (keywords == null) { throw new ArgumentNullException(nameof(keywords)); }

        var replayFilesQueryable = db.GetCollection<ReplayFile>("replayFile")
            .Include(r => r.FileInfo)
            .Include(r => r.Replay)
            .Include(r => r.ErrorInfo)
            .Include(r => r.Replay.BluePlayers)
            .Include(r => r.Replay.RedPlayers)
            .Query();

        replayFilesQueryable = sort switch 
        {
            SortMethod.NameAsc => replayFilesQueryable.OrderBy(x => _config.RenameFile ? x.FileInfo.Name : x.AlternativeName),
            SortMethod.NameDesc => replayFilesQueryable.OrderByDescending(x => _config.RenameFile ? x.FileInfo.Name : x.AlternativeName),
            SortMethod.DateAsc => replayFilesQueryable.OrderBy(x => x.FileInfo.CreationTime),
            SortMethod.DateDesc => replayFilesQueryable.OrderByDescending(x => x.FileInfo.CreationTime),
            SortMethod.SizeAsc => replayFilesQueryable.OrderBy(x => x.FileInfo.Size),
            SortMethod.SizeDesc => replayFilesQueryable.OrderByDescending(x => x.FileInfo.Size),
            _ => replayFilesQueryable.OrderBy(x => x.FileInfo.CreationTime)
        };

        if (keywords.Length > 0)
        {
            // this filters on ANY, we want to filter on ALL but not sure how to accomplish that (LiteDB issue #1885)
            replayFilesQueryable = replayFilesQueryable.Where("$.SearchKeywords[*] ANY IN @0", BsonMapper.Global.Serialize(keywords));
        }
        
        return replayFilesQueryable;
    }
    
    public void UpdateAlternativeName(string id, string newName)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(id);

        using var db = new LiteDatabase(_filePath);

        var replayFiles = db.GetCollection<ReplayFile>("replayFile")
            .Include("$.FileInfo")
            .Include("$.Replay")
            .Include("$.ErrorInfo")
            .Include("$.Replay.BluePlayers[*]")
            .Include("$.Replay.RedPlayers[*]");

        var result = replayFiles.FindById(id);

        if (result == null) return;

        _log.Information($"Db updating {result.AlternativeName} to {newName}");

        // Update the file results (for indexing/search)
        result.AlternativeName = newName;
        result.SearchKeywords = new List<string>
        {
            result.FileInfo.Name.ToUpper(CultureInfo.InvariantCulture),
            result.AlternativeName.ToUpper(CultureInfo.InvariantCulture)
        };
        replayFiles.Update(result);
    }
}