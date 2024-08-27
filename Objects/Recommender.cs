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
            var query = new InternalItemsQuery{
                SimilarTo = song,
                Limit = 3,
                IncludeItemTypes = [BaseItemKind.Audio]
            };

            var similarSongs = _libraryManager.GetItemList(query);
            Recommendations.AddRange(similarSongs.Select(song => new ScoredSong(song, user, _userDataManager)).ToList());
        }
        return Recommendations;
    }

}