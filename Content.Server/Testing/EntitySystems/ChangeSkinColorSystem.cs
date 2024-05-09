using Content.Server.Testing.Components;
using Content.Shared.Interaction.Events;
using Content.Server.Humanoid;
using Content.Shared.Humanoid;

namespace Content.Server.Testing.EntitySystems;
    
public sealed class ChangeSkinColorSystem : EntitySystem
{
	[Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
	
	public override void Initialize()
	{
		  base.Initialize();
		  SubscribeLocalEvent<ChangeSkinColorComponent, UseInHandEvent>(OnUseInHand);
	}

	private void OnUseInHand(EntityUid entity, ChangeSkinColorComponent comp, UseInHandEvent args)
	{
		if (TryComp<HumanoidAppearanceComponent>(entity, out var appcomp))
		{
			Logger.Info("Got event!");
			_humanoidAppearance.SetSkinColor(entity, Color.Brown, verify: false);
		}
	}
}
