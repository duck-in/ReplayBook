namespace Fraxiinus.ReplayBook.Files;

using Etirps.RiZhi;
using Fraxiinus.ReplayBook.Configuration.Models;
using Fraxiinus.ReplayBook.Files.Models;
using Fraxiinus.ReplayBook.Files.Models.Search;
using Fraxiinus.ReplayBook.Files.Repositories;
using Fraxiinus.Rofl.Extract.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class FileManager
{
    private readonly FolderRepository _fileSystem;
    private readonly DatabaseRepository _db;
    private readonly SearchRepository _search;

    private readonly RiZhi _log;
    private readonly List<string> _deletedFiles;
    private readonly List<string> _deletedFolders;
    private readonly ObservableConfiguration _config;
    private readonly ReplayReaderOptions _readerOptions;

    public FileManager(ObservableConfiguration config, RiZhi log)
    {
        _fileSystem = new FolderRepository(config, log);
        _db = new DatabaseRepository(config, log);
        _search = new SearchRepository(config, log);

        _log = log ?? throw new ArgumentNullException(nameof(log));
        _config = config;
        _deletedFiles = new List<string>();
        _deletedFolders = new List<string>();
        _readerOptions = new ReplayReaderOptions
        {
            LoadPayload = false,
            Type = ReplayType.Unknown
        };
    }

    public string DatabasePath { get => _db.GetDatabasePath(); }

    public string SearchIndexPath { get => _search.SearchIndexDirectory; }

    public void DeleteDatabase() => _db.DeleteDatabase();

    public void DeleteSearchIndex() => _search.DeleteIndex();

    /// <summary>
    /// This function is responsible for finding and loading in new replays
    /// </summary>
    public async Task<IEnumerable<ReplayErrorInfo>> InitialLoadAsync()
    {
        _log.Information("Starting initial load of replays");

        var errorResults = new List<ReplayErrorInfo>();

        // Get all files from all defined replay folders
        IReadOnlyCollection<FolderInfo> topLevelFolders = _fileSystem.GetTopLevelReplayFolders();

        foreach (FolderInfo folder in topLevelFolders)
        {
            errorResults.AddRange(await LoadFolder(folder));
        }

        _search.CommitIndex();
        _log.Information("Initial load of replays complete");
        return errorResults;
    }

    private async Task<IEnumerable<ReplayErrorInfo>> LoadFolder(FolderInfo folder)
    {
        var errorResults = new List<ReplayErrorInfo>();

        foreach (Models.FileInfo file in folder.Files)
        {
            if (file is FolderInfo subfolder)
            {
                errorResults.AddRange(await LoadFolder(subfolder));
                continue;
            }

            var temp = _db.GetReplayFile(file.Path);
            if (temp != null) continue;

            ReplayFile replayFile;
            
            try
            {
                var parseResult = await ReplayReader.ReadReplayAsync(file.Path, _readerOptions);
                var replay = new Replay(file.Path, parseResult);
                replayFile = new ReplayFile(file, replay);
            }
            catch (Exception ex)
            {
                // if parsing file failed for any reason, save info
                _log.Warning($"Failed to parse file: {file.Path}");
                _log.Warning(ex.ToString());

                var errorInfo = new ReplayErrorInfo(file.Path, ex.GetType().FullName, ex.ToString(), ex.StackTrace);
                replayFile = new ReplayFile(file, errorInfo);

                errorResults.Add(errorInfo);
            }

            _db.AddReplayFile(replayFile);
            _search.AddDocument(replayFile);
        }
        
        _db.AddFile(folder);
        
        return errorResults;
    }

    public async Task<ReplayFile> GetSingleFile(string path)
    {
        if (!File.Exists(path)) return null;

        ReplayFile returnValue = _db.GetReplayFile(path);

        // File exists in the database, return now
        if (returnValue != null)
        {
            _log.Information($"File {path} already exists in database. Match ID: {returnValue.Replay.MatchId}");
            return returnValue;
        }

        var replayFileInfo = _fileSystem.GetSingleReplayFileInfo(path);
        var parseResult = await ReplayReader.ReadReplayAsync(path, _readerOptions);

        var replayFile = new Replay(path, parseResult);
        var newResult = new ReplayFile(replayFileInfo, replayFile);

        _db.AddReplayFile(newResult);

        return newResult;
    }

    /// <summary>
    /// Checks all entries and deletes if they do not exist in the file system.
    /// </summary>
    /// <returns></returns>
    public void PruneDatabaseEntries()
    {
        _log.Information($"Pruning database...");

        var entries = _db.GetReplayFiles();

        foreach (var entry in entries)
        {
            // Files does not exist! (Technically this is the same as id, but it's more clear)
            // or File is not part of the current source folder collection 
            if (!File.Exists(entry.FileInfo.Path) || !_fileSystem.IsPathInSourceFolders(entry.FileInfo.Path))
            {
                _log.Information($"File {entry.Id} is no longer valid, removing from database...");
                _db.RemoveReplayFile(entry.Id);
            }
        }

        var folders = _db.GetAllFolders();

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder.Path) || !_fileSystem.IsPathInSourceFolders(folder.Path))
            {
                _log.Information($"Folder {folder.Path} is no longer valid, removing from database...");
                _db.RemoveFolder(folder.Path);
            }
        }
        
        _log.Information($"Pruning complete");
    }

    /// <summary>
    /// searchResultCount is -1 if results are direct from database
    /// </summary>
    /// <param name="searchParameters"></param>
    /// <param name="maxEntries"></param>
    /// <param name="skip"></param>
    /// <param name="resetSearch"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public (IReadOnlyCollection<ReplayFile>, int searchResultCount) GetReplays(SearchParameters searchParameters, int maxEntries, int skip, bool resetSearch = false)
    {
        if (searchParameters == null) { throw new ArgumentNullException(nameof(searchParameters)); }
        
        // If there is no query given...
        if (string.IsNullOrEmpty(searchParameters.QueryString))
        {
            // search the database
            return (_db.QueryReplayFiles([], searchParameters.SortMethod, maxEntries, skip), -1);
        }
        
        // otherwise search the SearchDatabase (?? what the fuck)
        var (results, searchResultCount) = _search.Query(searchParameters, maxEntries, skip, _config.SearchMinimumScore, resetSearch);

        return (_db.GetReplayFiles(results.Select(x => x.Id)), searchResultCount);
    }

    public IReadOnlyCollection<FolderInfo> GetTopLevelFolders(SearchParameters searchParameters, int maxEntries, int skip, bool resetSearch = false)
    {
        if (searchParameters == null) { throw new ArgumentNullException(nameof(searchParameters)); }
        
        // If there is no query given...
        if (string.IsNullOrEmpty(searchParameters.QueryString))
        {
            // search the database
            return _db.QueryTopLevelFolders([], searchParameters.SortMethod, maxEntries, skip);
        }
        
        // otherwise search the SearchDatabase (?? what the fuck)
        // var (results, searchResultCount) = _search.Query(searchParameters, maxEntries, skip, _config.SearchMinimumScore, resetSearch);
        // TODO implement search
        throw new NotImplementedException("TODO search repository");
    }
    
    public string RenameFolder(string path, string newName)
    {
        if (String.IsNullOrEmpty(newName)) throw new Exception("{EMPTY ERROR}");
        ArgumentNullException.ThrowIfNull(path);

        var newPath = Path.Combine(Path.GetDirectoryName(path), newName);

        if (Directory.Exists(newPath)) throw new Exception("{NAME ALREADY EXISTS ERROR}");

        _log.Information($"Renaming folder {path} -> {newPath}");
        // Rename the file
        try
        {
            Directory.Move(path, newPath);
        }
        catch (Exception ex)
        {
            _log.Information(ex.ToString());
            throw new Exception("{FAILED TO WRITE}", ex);
        }

        // delete the database entry
        _db.SetNewFolderId(path, newPath);
        // _search.RemoveDocument(replayFile.Id); TODO
        
        // return new file location
        return newPath;
    }
    
    public string RenameReplay(ReplayFile replayFile, string newName)
    {
        return _config.RenameFile
            ? RenameReplayInFileSystem(replayFile, newName)
            : RenameReplayInDatabase(replayFile, newName);
    }

    private string RenameReplayInDatabase(ReplayFile replayFile, string newName)
    {
        if (replayFile == null) throw new ArgumentNullException(nameof(replayFile));
        if (String.IsNullOrEmpty(newName)) throw new Exception("{EMPTY ERROR}");

        try
        {
            _db.UpdateAlternativeName(replayFile.Id, newName);
            _search.UpdateDocumentName(replayFile, newName);
        }
        catch (KeyNotFoundException ex)
        {
            _log.Information(ex.ToString());
            throw new Exception("{NOT FOUND ERROR}", ex);
        }

        // Return value file path, no changes made to filesystem so return same id
        return replayFile.Id;
    }

    private string RenameReplayInFileSystem(ReplayFile replayFile, string newName)
    {
        if (replayFile == null) throw new ArgumentNullException(nameof(replayFile));
        if (String.IsNullOrEmpty(newName)) throw new Exception("{EMPTY ERROR}");

        var nameWithExtension = newName.EndsWith(".rofl")
            ? newName
            : newName + ".rofl";

        var newPath = Path.Combine(Path.GetDirectoryName(replayFile.Id), nameWithExtension);

        _log.Information($"Renaming {replayFile.Id} -> {newPath}");
        // Rename the file
        try
        {
            File.Move(replayFile.Id, newPath);
        }
        catch (Exception ex)
        {
            _log.Information(ex.ToString());
            throw new Exception("{FAILED TO WRITE}", ex);
        }

        // delete the database entry
        _db.RemoveReplayFile(replayFile.Id);
        _search.RemoveDocument(replayFile.Id);

        // Update new values
        var fileInfo = replayFile.FileInfo;
        fileInfo.Name = nameWithExtension;
        fileInfo.Path = newPath;

        var replay = replayFile.Replay;
        replay.Name = nameWithExtension;
        replay.Id = newPath;

        var newFileResult = new ReplayFile(fileInfo, replay);
        _db.AddReplayFile(newFileResult);
        _search.AddDocument(newFileResult);
        _search.CommitIndex();

        // return new file location
        return newPath;
    }

    /// <summary>
    /// Doesn't actually delete, but moves it to the cache folder, in case they didnt mean to delete it
    /// </summary>
    /// <param name="replayFileram>
    /// <returns></returns>
    public string DeleteFile(ReplayFile replayFile)
    {
        if (replayFile == null) throw new ArgumentNullException(nameof(replayFile));
        
        
        _log.Information($"Moving {replayFile.Id} to cache folder - to be deleted when ReplayBook closes");

        var newPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "deletedReplays");
        Directory.CreateDirectory(newPath);

        newPath = Path.Combine(newPath, replayFile.FileInfo.Name + ".rofl");

        File.Move(replayFile.Id, newPath);

        _db.RemoveReplayFile(replayFile.Id);

        _deletedFiles.Add(newPath);
        return newPath;
    }

    public string DeleteFolder(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));

        _log.Information($"Moving folder {path} to cache folder - to be deleted when ReplayBook closes");

        var newPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "deletedFolders");
        Directory.CreateDirectory(newPath);

        newPath = Path.Combine(newPath, path.Split("\\").Last());

        Directory.Move(path, newPath);

        _db.RemoveFolder(path);

        _deletedFolders.Add(newPath);
        return newPath;
    }

    public void ClearDeletedFiles()
    {
        foreach (var file in _deletedFiles)
        {
            _log.Information($"Deleting file {file}");

            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        _deletedFiles.Clear();        
        
        foreach (var folder in _deletedFolders)
        {
            _log.Information($"Deleting folder {folder}");

            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
        
        _deletedFolders.Clear();    
    }
}
