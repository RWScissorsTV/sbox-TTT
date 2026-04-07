using Sandbox;

namespace TTT;

// WIP, not currently added to any shop.
[Category( "Equipment" )]
[ClassName( "ttt_equipment_poltergeist" )]
[Title( "Poltergeist" )]
public class Poltergeist : Weapon
{
	private SceneTraceResult _trace;

	public override void Simulate()
	{
		base.Simulate();

		_trace = Scene.Trace.Ray( Owner.EyePosition, Owner.EyePosition + Owner.EyeRotation.Forward * Player.MaxHintDistance )
			.IgnoreGameObject( GameObject )
			.IgnoreGameObject( Owner.GameObject )
			.Run();
	}

	protected override void AttackPrimary()
	{
		if ( !HasValidPlacement() || AmmoClip == 0 )
			return;

		AmmoClip--;
		Owner.SetAnimParameter( "b_attack", true );
		Sound.Play( Info.FireSound, Owner.EyePosition );
		AttachEnt();
	}

	private void AttachEnt()
	{
		var go = new GameObject( true, "PoltergeistEntity" );
		go.WorldPosition = _trace.EndPosition;
		var pEntity = go.Components.Create<PoltergeistEntity>();
		pEntity.AttachTo( _trace.GameObject );
		go.NetworkSpawn();
	}

	private bool HasValidPlacement()
	{
		return _trace.Hit && _trace.Body is not null;
	}
}
