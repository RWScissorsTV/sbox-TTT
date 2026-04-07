using System.Collections.Generic;
using Editor;
using Sandbox;

namespace TTT;

[Category( "Equipment" )]
[ClassName( "ttt_equipment_cigar" )]
[EditorModel( "models/cigar/cigar.vmdl" )]
[Title( "Cigar" )]
public class Cigar : Carriable
{
	public override List<UI.BindingPrompt> BindingPrompts => new()
	{
		new( InputAction.PrimaryAttack, "Smoke" )
	};

	private TimeUntil _timeUntilNextSmoke = 0;

	public override void Simulate()
	{
		if ( Input.Pressed( InputAction.PrimaryAttack ) && _timeUntilNextSmoke )
			Smoke();
	}

	private void Smoke()
	{
		_timeUntilNextSmoke = 5;

		var attachment = WorldRenderer?.GetAttachment( "muzzle" );
		if ( attachment.HasValue )
		{
			SceneParticles.PlayInstant( Scene, "particles/cigar/smokepuff", attachment.Value );
			SceneParticles.PlayInstant( Scene, "particles/muzzle/barrel_smoke", attachment.Value );
		}

		if ( Networking.IsHost )
		{
			var damageInfo = new DamageInfo()
				.WithDamage( 1 )
				.WithAttacker( Owner.GameObject )
				.WithWeapon( GameObject );

			Owner.TakeDamage( damageInfo );
		}
	}
}
