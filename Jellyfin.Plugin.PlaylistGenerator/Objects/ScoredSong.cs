using System.Runtime.InteropServices;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PlaylistGenerator.Objects;

// class for giving a song a score based on the user
public class ScoredSong : BaseItem
{
    private readonly IUserDataManager _userDataManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ActivityDatabase _activityDatabase;
    
    private int MaxPlaysSevenDays => _activityDatabase.MaxSevenDays;
    
    public BaseItem Song { get; set; }
    private User User { get; set; }
    public double Score { get; set; }
    public Guid AlbumId { get; set; }
    public Guid ArtistId { get; set; }
    public bool IsDisliked { get; set; }

    public ScoredSong(BaseItem song, User user, IUserDataManager userDataManager, ILibraryManager libraryManager, 
        ActivityDatabase activityDatabase)
    {
        _userDataManager = userDataManager;
        _libraryManager = libraryManager;
        _activityDatabase = activityDatabase;
        Song = song;
        User = user;
        Score = CalculateScore();
        AlbumId = song.ParentId;
        ArtistId = GetAristId(song);
        IsDisliked = DislikeStatus(); // skipped three or more times last month
    }
    
    private double GetNormalizedPlaysSevenDays()
    {
        if (Song.Id == Guid.Empty || User.Id == Guid.Empty || MaxPlaysSevenDays == 0)
        {
            return 0;
        }
        var songLengthSeconds = Song.RunTimeTicks / TimeSpan.TicksPerSecond;
        var sql = $"""
                  SELECT PlayDuration FROM PlaybackActivity
                  WHERE ItemId = '{Song.Id:N}' AND UserId = '{User.Id:N}' AND DateCreated > datetime('now', '-7 days')
                  """;
        var result = _activityDatabase.ExecuteQuery(sql);
        var plays = 0;
        foreach (var row in result)
        {
            plays += int.Parse(row["Column0"]) >= songLengthSeconds * 0.8 ? 1 : -1;
        }
        plays = Math.Max(plays, 0);
        return (double)plays / MaxPlaysSevenDays;
    }
    
    private int GetTotalPlays()
    {
        if (Song.Id == Guid.Empty || User.Id == Guid.Empty)
        {
            return 0;
        }
        var songLengthSeconds = Song.RunTimeTicks / TimeSpan.TicksPerSecond;
        var sql = $"""
                   SELECT PlayDuration FROM PlaybackActivity
                   WHERE ItemId = '{Song.Id:N}' AND UserId = '{User.Id:N}'
                   """;
        var result = _activityDatabase.ExecuteQuery(sql);
        var plays = 0;
        foreach (var row in result)
        {
            plays += int.Parse(row["Column0"]) >= songLengthSeconds * 0.8 ? 1 : -1;
        }
        plays = Math.Max(plays, 0);
        return plays;
    }
    
    // get artist id from album id
    private Guid GetAristId(BaseItem song)
    {
        if (song.ParentId == Guid.Empty)
        {
            return Guid.Empty;
        }
        var album = _libraryManager.GetItemById(song.ParentId) as MusicAlbum;
        return album == null ? Guid.Empty : _libraryManager.GetArtist(album.AlbumArtist).Id;
    }

    private bool DislikeStatus(int neededSkips = 3)
    {
        if (Score == 0.0) return false;
            
        var songLengthSeconds = Song.RunTimeTicks / TimeSpan.TicksPerSecond;
        
        var sql = $"""
                   SELECT PlayDuration FROM PlaybackActivity
                   WHERE ItemId = '{Song.Id:N}' AND UserId = '{User.Id:N}' AND DateCreated > datetime('now', '-30 days')
                   """;
        var result = _activityDatabase.ExecuteQuery(sql);
        var skipped = 0;
        foreach (var row in result)
        {
            skipped += int.Parse(row["Column0"]) >= songLengthSeconds * 0.8 ? 0 : 1;
        }
        
        return skipped >= neededSkips;
    }
    
    private double CalculateScore(double decayRate = 0.5, List<double>? weights = null, int minPlayThreshold = 3)
    {
        weights ??= [0.6, 0.25, 0.15];
        var userData = _userDataManager.GetUserData(User, Song);

        // songs that the user barely knows (below the minPlayThreshold) should get a score of zero
        if (userData.PlayCount < minPlayThreshold)
        {
            return 0.0;
        }
        
        // I do this because jellyfin scrobble data is sometimes wildly overestimated.
        // Better to rely on what we actually observe.
        var observedPlayCount = GetTotalPlays(); 
        var frequency = GetNormalizedPlaysSevenDays();

        // how long it's been since they last listened to it
        double recency = 0;
        if (userData.LastPlayedDate != null)
        {
            var timeSpan = (TimeSpan)(DateTime.Now - userData.LastPlayedDate);
            var daysSinceLastPlayed = Math.Max(timeSpan.Days, 0);
            recency = (1 / (1 + Math.Exp(decayRate * daysSinceLastPlayed)));
        }
        
        // songs that have been listened to a lot may not be super wanted anymore
        var highPlayDecay = 1 / (1 + Math.Log(1 + observedPlayCount, 2));
        return weights[0] * frequency + weights[1] * recency + weights[2] * highPlayDecay;
    }
}