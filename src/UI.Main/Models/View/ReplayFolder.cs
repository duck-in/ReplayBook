using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Fraxiinus.ReplayBook.Configuration.Models;
using Fraxiinus.ReplayBook.Executables.Old;
using Fraxiinus.ReplayBook.Files.Models;
using Fraxiinus.ReplayBook.UI.Main.Extensions;
using Lucene.Net.Analysis.Hunspell;

namespace Fraxiinus.ReplayBook.UI.Main.Models.View;

public class ReplayFolder : ReplayNode
{

    public ReplayFolder(FolderInfo directory, ObservableConfiguration configuration, Dictionary<string, ReplayFile> fileResults, ExecutableManager executableManager)
    {
        if (directory == null) { throw new ArgumentNullException(nameof(directory)); }
        
        Name = directory.Name;
        Location = directory.Path;
        Children = new ObservableCollection<ReplayNode>();

        foreach (var folder in directory.Files.OfType<FolderInfo>()) Children.Add(new ReplayFolder(folder, configuration, fileResults, executableManager));
        foreach (var file in directory.ReplayFiles)
        {
            fileResults.TryAdd(file.Id, file);
            var preview = new ReplayPreview(file, configuration);
            preview.IsSupported = executableManager.DoesVersionExist(preview.GameVersion);
            Children.Add(preview);
        }
    }

    private string _name;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string ReplayCount => Children.Aggregate(0, (acc, c) => acc + (c is ReplayFolder rf ? rf.ReplayCount.ToInt() : 1)).ToString(); 

    public ObservableCollection<ReplayNode> Children { get; }
}