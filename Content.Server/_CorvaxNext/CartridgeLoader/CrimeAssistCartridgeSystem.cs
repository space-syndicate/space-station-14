using Content.Shared.CartridgeLoader;
using Content.Server._CorvaxNext.CartridgeLoader;
using Content.Server.CartridgeLoader.Cartridges;
using Content.Server.CartridgeLoader;

namespace Content.Server._CorvaxNext.CartridgeLoader.Cartridges;

public sealed class CrimeAssistCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem? _cartridgeLoaderSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
    }
}
