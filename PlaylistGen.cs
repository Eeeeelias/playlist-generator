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
using Jellyfin.Data.Enums;

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


public class PlaylistGenerationTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<PlaylistGenerationTask> _logger;
    private readonly PluginConfiguration _config;

    public PlaylistGenerationTask(ILibraryManager libraryManager, ILogger<PlaylistGenerationTask> logManager)
    {
        _libraryManager = libraryManager;
        _logger = logManager;
        _config = Plugin.Instance!.Configuration;
    }

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

        foreach (var song in songs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            songList.Add(new ScoredSong(song, _config.PlaylistUser));
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
    public BaseItem Song { get; set; }
    public string User { get; set; }
    public double Score { get; set; }

    public ScoredSong(BaseItem song, string user)
    {
        Song = song;
        User = user;
        Score = CalculateScore();
    }

    private double CalculateScore()
    {
        return new Random().NextDouble();
    }
}