using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Playlists;
using MediaBrowser.Controller.Playlists;

using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;

namespace PlaylistGenerator.Objects;

// Service to create and delete playlists
public class PlaylistService(IPlaylistManager playlistManager, ILibraryManager libraryManager)
{
    private readonly IPlaylistManager _playlistManager = playlistManager;
    private readonly ILibraryManager _libraryManager = libraryManager;

    public static List<ScoredSong> AssemblePlaylist(List<ScoredSong> songs, int maxLength)
    {
        int maxLengthSeconds = maxLength * 60;
        int totalSeconds = 0;
        int i = 0;
        HashSet<Guid> seenGuids = []; 
        List<ScoredSong> assembledPlaylist = [];
        while (totalSeconds < maxLengthSeconds && i < songs.Count)
        {   
            // make sure song is valid and is not already in the playlist
            if (songs[i].Song.RunTimeTicks == null || seenGuids.Contains(songs[i].Song.Id))
            {
                i++;
                continue;
            }

            assembledPlaylist.Add(songs[i]);
            totalSeconds += (int)((long)(songs[i].Song.RunTimeTicks ?? 0) / 10_000_000);
            seenGuids.Add(songs[i].Song.Id);
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
