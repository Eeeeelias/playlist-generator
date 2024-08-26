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
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Net;

using Jellyfin.Data.Entities;

using Jellyfin.Data.Enums;
using Microsoft.Extensions.Configuration;

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


public class PlaylistGenerationTask(ILibraryManager libraryManager, IUserManager userManager, IUserDataManager userDataManager, ILogger<PlaylistGenerationTask> logManager) : IScheduledTask
{
    private readonly ILibraryManager _libraryManager = libraryManager;
    private readonly ILogger<PlaylistGenerationTask> _logger = logManager;
    private readonly PluginConfiguration _config = Plugin.Instance!.Configuration;
    private readonly IUserManager _userManager = userManager;
    private readonly IUserDataManager _userDataManager = userDataManager;


    public string Name => "Generate personal playlist";
    public string Key => "PlaylistGenerationTask";
    public string Description => "Generate a library based on previous listen data + similarity.";
    public string Category => "Library";

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); 

        _logger.LogInformation("Start generating playlist");

        var songList = new List<ScoredSong>();
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Audio]
        };
        
        var songs = _libraryManager.GetItemList(query);

        if (songs.Count <= 0)
        {
            _logger.LogWarning("No music found.");
            return Task.CompletedTask;
        }

        _logger.LogInformation($"Found {songs.Count} songs");
        
        var currentUser = _userManager.GetUserByName(_config.PlaylistUserName);

        if (currentUser == null)
        {
            _logger.LogWarning($"User: {_config.PlaylistUserName} not found. Aborting.");
            return Task.CompletedTask;
        }

        foreach (var song in songs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            songList.Add(new ScoredSong(song, currentUser, _userDataManager));
        }

        List<ScoredSong> sortedSongs = songList.OrderByDescending(song => song.Score).ToList();
        foreach (var scoredSong in sortedSongs.Take(5))
        {
            _logger.LogInformation($"{scoredSong.Song.Name} - Score: {scoredSong.Score}");
        }

        _logger.LogInformation("Generated playlist.");
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


public class ScoredSong : BaseItem
{
    private readonly IUserDataManager _userDataManager;
    public BaseItem Song { get; set; }
    public User User { get; set; }
    public double Score { get; set; }

    

    public ScoredSong(BaseItem song, User user, IUserDataManager userDataManager)
    {
        _userDataManager = userDataManager;
        Song = song;
        User = user;
        Score = CalculateScore();
    }

    private double CalculateScore()
    {
        return new Random().NextDouble();
    }
}