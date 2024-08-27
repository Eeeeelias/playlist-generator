using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;
using Jellyfin.Data.Entities;


namespace PlaylistGenerator.Objects;

// class for giving a song a score based on the user
public class ScoredSong : BaseItem
{
    private readonly IUserDataManager _userDataManager;
    public BaseItem Song { get; set; }
    private User User { get; set; }
    public double Score { get; set; }
    public Guid AlbumId { get; set; }
    public Guid ArtistId { get; set; }

    public ScoredSong(BaseItem song, User user, IUserDataManager userDataManager)
    {
        _userDataManager = userDataManager;
        Song = song;
        User = user;
        Score = CalculateScore();
        AlbumId = song.ParentId;
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
        double favourite = 0.0;
        if (userData.IsFavorite)
        {
            favourite = 1.0;
        }

        // how long it's been since they last listened to it
        double recency = 0;
        if (userData.LastPlayedDate != null)
        {
            TimeSpan timeSpan = (TimeSpan)(userData.LastPlayedDate - DateTime.Now);
            int daysSinceLastPlayed = timeSpan.Days;
            recency = (1 / (1 + Math.Exp(decayRate * daysSinceLastPlayed))) + 0.5;
        }
        
        // songs that have been listened to a lot may not be super wanted anymore
        double highPlayDecay = 1 / 1+ Math.Log(userData.PlayCount, 2); 

        return weights[0] * favourite + weights[1] * recency + weights[2] * highPlayDecay;
    }
}