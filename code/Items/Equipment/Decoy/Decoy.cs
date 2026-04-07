using Sandbox;

namespace TTT;

[Category( "Equipment" )]
[ClassName( "ttt_equipment_decoy" )]
[HideInEditor]
[Title( "Decoy" )]
public class Decoy : Deployable
{
	protected override void OnDeploy()
	{
		var entity = Components.GetOrCreate<DecoyEntity>();
		entity.Initialize( PreviousOwner );

		var decoyComponent = PreviousOwner.Components.GetOrCreate<DecoyComponent>();
		decoyComponent.Decoy = entity;
	}
}

public partial class DecoyComponent : Component
{
	public DecoyEntity Decoy { get; set; }
}
