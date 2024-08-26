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

        var songList = new List<ScoredSong>();
        var SongQuery = new InternalItemsQuery{IncludeItemTypes = [BaseItemKind.Audio]};
        
        var songs = _libraryManager.GetItemList(SongQuery);

        if (songs.Count <= 0)
        {
            _logger.LogWarning("No music found.");
            return Task.CompletedTask;
        }

        _logger.LogInformation($"Found {songs.Count} songs");
        
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

        List<ScoredSong> sortedSongs = songList.OrderByDescending(song => song.Score).ToList();
        foreach (var scoredSong in sortedSongs.Take(5))
        {
            _logger.LogInformation($"{scoredSong.Song.Name} - Score: {scoredSong.Score}");
        }

        // check if playlist exists
        PlaylistService playlistServer = new(_playlistManager, _libraryManager);
        var allPlaylists = _libraryManager.GetItemList(new InternalItemsQuery{IncludeItemTypes = [BaseItemKind.Playlist]});

        if (allPlaylists.Any(playlist => playlist.Name.Equals(_config.PlaylistName))) 
        {
            _logger.LogInformation($"Playlist {_config.PlaylistName} exists. Overwriting.");
            playlistServer.RemovePlaylist(_config.PlaylistName);
        }

        // make the playlist
        playlistServer.CreatePlaylist(_config.PlaylistName, currentUser, sortedSongs[0..20]);

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
        var userData = _userDataManager.GetUserData(User.Id, Song);
        Score = new Random().NextDouble();
        if (userData.IsFavorite)
        {
            Console.WriteLine($"Likes: {userData.Likes}\nPlayCount: {userData.PlayCount
            }\nLast Played:{userData.LastPlayedDate}\nFavorite:{userData.IsFavorite}");
            Score++;
        }
        return Score;
    }
}


public class PlaylistService
{
    private readonly IPlaylistManager _playlistManager;
    private readonly ILibraryManager _libraryManager;

    public PlaylistService(IPlaylistManager playlistManager, ILibraryManager libraryManager)
    {
        _playlistManager = playlistManager;
        _libraryManager = libraryManager;
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
        try 
        {
            Console.WriteLine(request.ItemIdList[0]);
            Console.WriteLine(request.ItemIdList[1]);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Console.WriteLine($"Playlist not long enough. Songs: {items.Count}");
        }


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
            var options = new DeleteOptions();
            _libraryManager.DeleteItem(playlist, options);
        }
    }
}