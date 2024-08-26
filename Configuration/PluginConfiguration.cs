using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Plugins;

namespace PlaylistGenerator.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        PlaylistName = "My Personal Mix";
        PlaylistDuration = 600;
        PlaylistUser = "elias";
    }

    public int PlaylistDuration { get; set; }

    public string PlaylistName { get; set; }

    public string PlaylistUser { get; set; }
}
