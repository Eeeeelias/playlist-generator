using System;
using System.Collections.Generic;
using System.Globalization;
using PlaylistGenerator.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Controller.Library;

namespace PlaylistGenerator;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{

    private ILogger<Plugin> _logger;

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _logger = logger;
    }

    public override string Name => "PlaylistGenerator";

    public override Guid Id => Guid.Parse("975dde10-724f-4b72-8efc-91a1cb2d9510");

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = this.Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        };
    }
}


public class LibraryListingTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<LibraryListingTask> _logger;
    private readonly PluginConfiguration _config;

    public LibraryListingTask(ILibraryManager libraryManager, ILogger<LibraryListingTask> logManager)
    {
        _libraryManager = libraryManager;
        _logger = logManager;
        _config = Plugin.Instance!.Configuration;
    }

    public string Name => "List Libraries and Config";
    public string Key => "LibraryListingTask";
    public string Description => "Lists all libraries and current configuration values.";
    public string Category => "Library";

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
     
        _logger.LogInformation("LibraryListingTask started.");

        var libraries = _libraryManager.GetVirtualFolders();
        bool hasMusicLibrary = libraries.Any(library => library.CollectionType == MediaBrowser.Model.Entities.CollectionTypeOptions.music);

        if (hasMusicLibrary != true)
        {
            _logger.LogWarning("No music library found. Aborting");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Now doing cool stuff");

        _logger.LogInformation("LibraryListingTask finished.");
        return Task.CompletedTask;
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            // Example trigger: Run every day at midnight
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerDaily,
                TimeOfDayTicks = TimeSpan.FromHours(0).Ticks
            }
        };
    }
}