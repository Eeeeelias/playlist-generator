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
using MediaBrowser.Model.Playlists;

using Jellyfin.Data.Entities;

using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Controller.Providers;
using J2N;

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


public class PlaylistGenerationTask(ILibraryManager libraryManager, 
                                    IUserManager userManager, 
                                    IUserDataManager userDataManager, 
                                    ILogger<PlaylistGenerationTask> logManager,
                                    IPlaylistManager playlistManager) : IScheduledTask
{
    private readonly ILibraryManager _libraryManager = libraryManager;
    private readonly ILogger<PlaylistGenerationTask> _logger = logManager;
    private readonly PluginConfiguration _config = Plugin.Instance!.Configuration;
    private readonly IUserManager _userManager = userManager;
    private readonly IUserDataManager _userDataManager = userDataManager;
    private readonly IPlaylistManager _playlistManager = playlistManager;


    public string Name => "Generate personal playlist";
    public string Key => "PlaylistGenerationTask";
    public string Description => "Generate a library based on previous listen data + similarity.";
    public string Category => "Library";

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); 

        _logger.LogInformation("Start generating playlist");
        
        // first get all songs
        var songList = new List<ScoredSong>();
        var SongQuery = new InternalItemsQuery{IncludeItemTypes = [BaseItemKind.Audio]};
        
        var songs = _libraryManager.GetItemList(SongQuery);

        if (songs.Count <= 0)
        {
            _logger.LogWarning("No music found.");
            return Task.CompletedTask;
        }

        _logger.LogInformation($"Found {songs.Count} songs");
        
        // get user to identify listen data
        User? currentUser = _userManager.GetUserByName(_config.PlaylistUserName);

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

        // assemble the playlist
        PlaylistService playlistServer = new(_playlistManager, _libraryManager);
        List<ScoredSong> sortedSongs = [.. songList.OrderByDescending(song => song.Score)];
        var assembledPlaylist = PlaylistService.AssemblePlaylist(sortedSongs, _config.PlaylistDuration);
            

        // check if playlist exists
        var allPlaylists = _libraryManager.GetItemList(new InternalItemsQuery{IncludeItemTypes = [BaseItemKind.Playlist]});

        if (allPlaylists.Any(playlist => playlist.Name.Equals(_config.PlaylistName))) 
        {
            _logger.LogInformation($"Playlist {_config.PlaylistName} exists. Overwriting.");
            playlistServer.RemovePlaylist(_config.PlaylistName);
        }

        // make the playlist
        playlistServer.CreatePlaylist(_config.PlaylistName, currentUser, assembledPlaylist);

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


// class for giving a song a score based on the user
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

    private double CalculateScore(double decayRate = 0.5, List<double>? weights = null, int minPlayThreshold = 3)
    {
        weights ??= [0.4, 0.35, 0.25];
        var userData = _userDataManager.GetUserData(User.Id, Song);

        // songs that the user barely knows (below the minPlayThreshold) should get a score of zero
        if (userData.PlayCount < minPlayThreshold)
        {
            return 0.0;
        }

        // information about if the user likes this song
        int favourite = 0;
        if (userData.IsFavorite)
        {
            favourite = 1;
        }

        // how long it's been since they last listened to it
        double recency = 0;
        if (userData.LastPlayedDate != null)
        {
            TimeSpan timeSpan = (TimeSpan)(userData.LastPlayedDate - DateTime.Now);
            int daysSinceLastPlayed = timeSpan.Days;
            recency = 1 / (1 + Math.Exp(decayRate * daysSinceLastPlayed));
        }
        
        // songs that have been listened to a lot may not be super wanted anymore
        double highPlayDecay = 1 / 1+ Math.Log(1+userData.PlayCount, 2); 
        
        return weights[0] * favourite + weights[1] * recency + weights[2] * highPlayDecay;
    }
}


public class Recommender
{
    private readonly double _explorationCoefficient;

    public Recommender(double explorationCoefficient)
    {
        _explorationCoefficient = explorationCoefficient;
    }


}


// Service to create and delete playlists
public class PlaylistService
{
    private readonly IPlaylistManager _playlistManager;
    private readonly ILibraryManager _libraryManager;

    public PlaylistService(IPlaylistManager playlistManager, ILibraryManager libraryManager)
    {
        _playlistManager = playlistManager;
        _libraryManager = libraryManager;
    }

    public static List<ScoredSong> AssemblePlaylist(List<ScoredSong> songs, int maxLength)
    {
        int maxLengthSeconds = maxLength * 60;
        int totalSeconds = 0;
        int i = 0;
        List<ScoredSong> assembledPlaylist = [];
        while (totalSeconds < maxLengthSeconds && i < songs.Count)
        {   
            if (songs[i].Song.RunTimeTicks == null)
            {
                i++;
                continue;
            }
            assembledPlaylist.Add(songs[i]);
            totalSeconds += (int)((long)(songs[i].Song.RunTimeTicks ?? 0) / 10_000_000);
            i++;
        }
        if (totalSeconds > maxLengthSeconds)
        {
            Console.WriteLine($"Stopped because of tick length: {totalSeconds} vs {maxLengthSeconds}");
        }
        else
        {
            Console.WriteLine("Stopped because of song count");
        }
        return assembledPlaylist;
    }

    public void CreatePlaylist(string playlistName, User user, List<ScoredSong> items)
    {

        // Create the playlist
        var request = new PlaylistCreationRequest
        {
            Name = playlistName,
            ItemIdList = items.Select(item => item.Song.Id).ToArray(),
            MediaType = MediaType.Audio,
            UserId = user.Id
        };
        var playlist = _playlistManager.CreatePlaylist(request);
    }

    public void RemovePlaylist(string playlistName)
    {
        // Find the playlist by name
        var playlists = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Playlist]
        });

        var playlist = playlists.FirstOrDefault(p => p.Name.Equals(playlistName));

        if (playlist != null)
        {
            // Delete the playlist
            var options = new DeleteOptions{DeleteFileLocation = true};
            _libraryManager.DeleteItem(playlist, options);
        }
    }
}
