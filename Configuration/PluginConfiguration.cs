using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Plugins;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Library;


namespace PlaylistGenerator.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        PlaylistName = "My Personal Mix";
        PlaylistDuration = 600;
        PlaylistUserName = "elias";
    }

    public int PlaylistDuration { get; set; }

    public string PlaylistName { get; set; }

    public string PlaylistUserName { get; set; }
}
