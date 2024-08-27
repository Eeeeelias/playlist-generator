using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;

using Jellyfin.Data.Entities;

using Jellyfin.Data.Enums;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;

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
        foreach (ScoredSong song in songBasis)
        {
            break;
        }

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
        // DEBUG stuff
        foreach (string genre in allGenres)
        {
            Console.WriteLine(genre);
        }
        
        var query = new InternalItemsQuery
        {
            Genres = [.. allGenres],
            Limit = 50,
            IncludeItemTypes = [BaseItemKind.Audio]
        };

        var similarSongs = _libraryManager.GetItemList(query);
        List<ScoredSong> potentialSongs = similarSongs.Select(song => new ScoredSong(song, user, _userDataManager)).ToList();
        // Todo incorporate exploration coefficient to remove songs with too low of a score
        switch (_explorationCoefficient)
        {
            case 0: potentialSongs = potentialSongs.Where(song => song.Score > 0).ToList(); break;
            case 1: potentialSongs = potentialSongs.Where(song => song.Score > 0).ToList(); break;
            case 2: potentialSongs = potentialSongs.Where(song => song.Score > 0).ToList(); break;
            case 3: potentialSongs = potentialSongs.Where(song => song.Score > 0).ToList(); break;
            case 4: potentialSongs = potentialSongs.Where(song => song.Score > 0).ToList(); break;
            case 5: potentialSongs = potentialSongs.Where(song => song.Score < 0.5).ToList(); break;
            default: break;
        }

        if (_explorationCoefficient == 0)
        {
            potentialSongs = potentialSongs.Where(song => song.Score > 0).ToList();
        }
        Recommendations.AddRange(potentialSongs);
        return Recommendations;
    }
}