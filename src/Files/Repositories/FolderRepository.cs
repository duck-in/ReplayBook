using Etirps.RiZhi;
using Fraxiinus.ReplayBook.Configuration.Models;
using Fraxiinus.ReplayBook.Files.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileInfo = Fraxiinus.ReplayBook.Files.Models.FileInfo;

namespace Fraxiinus.ReplayBook.Files.Repositories
{
    /// <summary>
    /// Watches given folders defined in config file for new replay files
    /// Once a new file is detected, parse the data and place it in the database
    /// (depends on if database supports on update)
    /// ViewModel asks the database(wrapper???) for the replay data
    /// </summary>
    public class FolderRepository
    {

        private readonly ObservableConfiguration _config;
        private readonly RiZhi _log;

        public FolderRepository(ObservableConfiguration config, RiZhi log)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Returns full paths of every replay file to show
        /// </summary>
        /// <returns></returns>
        public FolderInfo[] GetTopLevelReplayFolders()
        {
            List<FolderInfo> topLevelFolders = new List<FolderInfo>();

            topLevelFolders.AddRange(_config.ReplayFolders
                .Select(f => new DirectoryInfo(f))
                .Select(d => new FolderInfo(d, 0)));

            return topLevelFolders.ToArray();
        }

        public FileInfo GetSingleReplayFileInfo(string path)
        {
            var file = new System.IO.FileInfo(path);

            // If the file is not supported, skip it
            if (!file.Name.EndsWith(".rofl", StringComparison.OrdinalIgnoreCase))
            {
                _log.Warning($"File {path} is not supported, cannot get file info");
                return null;
            }

            return new FileInfo(file);
        }

        public bool IsPathInSourceFolders(string path)
        {
            return _config.ReplayFolders.Any(x => path.StartsWith(x, StringComparison.OrdinalIgnoreCase));
        }
    }
}
