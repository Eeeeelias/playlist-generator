using System;
using System.Collections.Generic;
using System.Globalization;
using PlaylistGenerator.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

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