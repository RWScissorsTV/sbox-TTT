using Editor;
using Sandbox;

namespace TTT;

[Category( "Weapons" )]
[ClassName( "ttt_weapon_bekas" )]
[EditorModel( "models/weapons/w_bekas.vmdl" )]
[Title( "Bekas-M" )]
public partial class Bekas : Weapon
{
	private bool _attackedDuringReload = false;

	public override void ActiveStart( Player player )
	{
		base.ActiveStart( player );

		_attackedDuringReload = false;
		TimeSinceReload = 0f;
	}

	protected override bool CanReload()
	{
		if ( !base.CanReload() )
			return false;

		var rate = Info.PrimaryRate;
		if ( rate <= 0 )
			return true;

		return TimeSincePrimaryAttack > (1 / rate);
	}

	public override void Simulate()
	{
		base.Simulate();

		if ( IsReloading && Input.Pressed( InputAction.PrimaryAttack ) )
			_attackedDuringReload = true;
	}

	protected override void OnReloadFinish()
	{
		IsReloading = false;

		TimeSincePrimaryAttack = 0;

		AmmoClip += TakeAmmo( 1 );

		if ( !_attackedDuringReload && AmmoClip < Info.ClipSize && Owner.AmmoCount( Info.AmmoType ) > 0 )
			Reload();
		else
			BroadcastFinishReload();

		_attackedDuringReload = false;
	}

	[Rpc.Broadcast]
	protected void BroadcastFinishReload()
	{
		ViewModelRenderer?.Set( "reload_finished", true );
	}
}

