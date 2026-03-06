using Content.Shared.GameTicking.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [ViewVariables]
    public ProtoId<LobbyBackgroundPrototype>? LobbyBackground { get; private set; }

    [ViewVariables]
    private List<ProtoId<LobbyBackgroundPrototype>>? _lobbyBackgrounds;

    [ViewVariables]
    private List<ProtoId<LobbyBackgroundPrototype>>? _lobbyVideoBackgrounds;

    private static readonly string[] WhitelistedBackgroundExtensions = new string[] {"png", "jpg", "jpeg", "webp"};

    private static readonly string[] WhitelistedVideoExtensions = new string[] {"webm", "mp4", "ogg", "ogv"};

    private void InitializeLobbyBackground()
    {
        var allprotos = _prototypeManager.EnumeratePrototypes<LobbyBackgroundPrototype>().ToList();
        _lobbyBackgrounds ??= new List<ProtoId<LobbyBackgroundPrototype>>();
        _lobbyVideoBackgrounds ??= new List<ProtoId<LobbyBackgroundPrototype>>();

        //create protoids from them
        foreach (var proto in allprotos)
        {
            // Check for video first
            if (proto.Video != null)
            {
                var ext = proto.Video?.Extension;
                if (ext == null || !WhitelistedVideoExtensions.Contains(ext))
                    continue;

                // Add to video backgrounds list
                _lobbyVideoBackgrounds.Add(new ProtoId<LobbyBackgroundPrototype>(proto.ID));

                // Also add to general list (for fallback)
                _lobbyBackgrounds.Add(new ProtoId<LobbyBackgroundPrototype>(proto.ID));
            }
            // Check for static image
            else if (proto.Background != null)
            {
                var ext = proto.Background?.Extension;
                if (ext == null || !WhitelistedBackgroundExtensions.Contains(ext))
                    continue;

                // Add to general list only
                _lobbyBackgrounds.Add(new ProtoId<LobbyBackgroundPrototype>(proto.ID));
            }
            // Skip prototypes that have neither Background nor Video
        }

        RandomizeLobbyBackground();
    }

    private void RandomizeLobbyBackground()
    {
        // First try to pick from video backgrounds if available
        if (_lobbyVideoBackgrounds != null && _lobbyVideoBackgrounds.Count != 0)
        {
            LobbyBackground = _robustRandom.Pick(_lobbyVideoBackgrounds);
        }
        // Fall back to static images if no video backgrounds available
        else if (_lobbyBackgrounds != null && _lobbyBackgrounds.Count != 0)
        {
            LobbyBackground = _robustRandom.Pick(_lobbyBackgrounds);
        }
        else
        {
            LobbyBackground = null;
        }
    }
}
