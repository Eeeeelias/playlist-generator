using PlaylistGenerator.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;

using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Playlists;
using PlaylistGenerator.Objects;

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
        Recommender playlistRecommender = new(_libraryManager, _userDataManager, 5);
        List<ScoredSong> topSongs = [.. songList.OrderByDescending(song => song.Score).Take(20)];
        List<ScoredSong> similarBySong = playlistRecommender.RecommendSimilar(topSongs, currentUser);
        List<ScoredSong> similarByGenre = playlistRecommender.RecommendByGenre(topSongs, currentUser);

        List<ScoredSong> allSongs = [..topSongs];
        allSongs.AddRange(similarBySong);
        allSongs.AddRange(similarByGenre);

        _logger.LogInformation($"Highest score: {allSongs[0].Score} for song: {allSongs[0].Song.Name}");
        var assembledPlaylist = PlaylistService.AssemblePlaylist(allSongs, _config.PlaylistDuration);
            

        // check if playlist exists
        var allPlaylists = _libraryManager.GetItemList(new InternalItemsQuery{IncludeItemTypes = [BaseItemKind.Playlist]});

        if (allPlaylists.Any(playlist => playlist.Name.Equals(_config.PlaylistName))) 
        {
            _logger.LogInformation($"Playlist {_config.PlaylistName} exists. Overwriting.");
            playlistServer.RemovePlaylist(_config.PlaylistName);
        }

        // make the playlist
        playlistServer.CreatePlaylist(_config.PlaylistName, currentUser, assembledPlaylist);

        _logger.LogInformation($"Generated personal playlist for {currentUser.Username}.");
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

