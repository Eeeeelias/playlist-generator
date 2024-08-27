using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;

using Jellyfin.Data.Entities;

using Jellyfin.Data.Enums;

namespace PlaylistGenerator.Objects;


public class Recommender(ILibraryManager libraryManager, IUserDataManager userDataManager, double explorationCoefficient = 3)
{
    private readonly double _explorationCoefficient = explorationCoefficient;
    private readonly IUserDataManager _userDataManager = userDataManager;
    private readonly ILibraryManager _libraryManager = libraryManager;

    public List<ScoredSong> RecommendSimilar(List<ScoredSong> songBasis, User user)
    {
        List<ScoredSong> Recommendations = [];
        foreach (ScoredSong song in songBasis)
        {
            var query = new InternalItemsQuery
            {
                SimilarTo = song.Song,
                Limit = 3,
                IncludeItemTypes = [BaseItemKind.Audio]
            };

            var similarSongs = _libraryManager.GetItemList(query);
            Recommendations.AddRange(similarSongs.Select(song => new ScoredSong(song, user, _userDataManager)).ToList());
        }
        return Recommendations;
    }

    // songs by the same artist are more similar than songs of the same genre (in general)
    public List<ScoredSong> RecommendByArtist(List<ScoredSong> songBasis, User user)
    {
        List<ScoredSong> Recommendations = [];
        HashSet<Guid> allArtists = [];
        foreach (ScoredSong song in songBasis)
        {
            // TODO: Add artists here
            break;
        }

        var query = new InternalItemsQuery
        {
            ArtistIds = [.. allArtists],
            Limit = 50,
            IncludeItemTypes = [BaseItemKind.Audio]
        };

        var similarSongs = _libraryManager.GetItemList(query);
        List<ScoredSong> potentialSongs = similarSongs.Select(song => new ScoredSong(song, user, _userDataManager)).ToList();
        potentialSongs = FilterByExploration(potentialSongs);
        Recommendations.AddRange(potentialSongs);

        return Recommendations;
    }

    public List<ScoredSong> RecommendByGenre(List<ScoredSong> songBasis, User user)
    {
        List<ScoredSong> Recommendations = [];
        HashSet<string> allGenres = [];
        foreach (ScoredSong song in songBasis)
        {
            var genres = song.Song.Genres;
            allGenres.UnionWith(genres);
        }
        
        var query = new InternalItemsQuery
        {
            Genres = [.. allGenres],
            Limit = 50,
            IncludeItemTypes = [BaseItemKind.Audio]
        };

        var similarSongs = _libraryManager.GetItemList(query);
        List<ScoredSong> potentialSongs = similarSongs.Select(song => new ScoredSong(song, user, _userDataManager)).ToList();
        potentialSongs = FilterByExploration(potentialSongs);

        Recommendations.AddRange(potentialSongs);
        return Recommendations;
    }

    private List<ScoredSong> FilterByExploration(List<ScoredSong> potentialSongs)
    {
        List<ScoredSong> filteredSongs = [];
        double minScore = potentialSongs.Min(song => song.Score);
        double maxScore = potentialSongs.Max(song => song.Score);
        filteredSongs = _explorationCoefficient switch
        {
            1 => potentialSongs.Where(song => song.Score > maxScore / 2).ToList(),
            2 => potentialSongs.Where(song => song.Score > Math.Min(maxScore, 0.2) / 4).ToList(),
            3 => potentialSongs,
            4 => potentialSongs.Where(song => song.Score < Math.Max(minScore, 0.2) * 4).ToList(),
            5 => potentialSongs.Where(song => song.Score < minScore * 2).ToList(),
            _ => potentialSongs,
        };
        return filteredSongs;
    }
}