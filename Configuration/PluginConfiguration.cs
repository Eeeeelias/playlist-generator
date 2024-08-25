using MediaBrowser.Model.Plugins;

namespace PlaylistGenerator.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        PlaylistName = "My Personal Mix";
        PlaylistDuration = 600;
    }

    public int PlaylistDuration { get; set; }

    public string PlaylistName { get; set; }
}
