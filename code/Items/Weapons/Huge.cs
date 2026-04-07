using Editor;
using System;

namespace TTT;

[Category( "Weapons" )]
[ClassName( "ttt_weapon_huge" )]
[EditorModel( "models/weapons/w_mg4.vmdl" )]
[Title( "H.U.G.E" )]
public class Huge : Weapon
{
	private const string BulletsBodyGroup = "bullets";
	private const int MaxBulletsChoice = 7;

	public override void Simulate()
	{
		base.Simulate();

		// As ammo decreases, update the viewmodel "bullets" body group.
		ViewModelRenderer?.SetBodyGroup( BulletsBodyGroup, Math.Min( AmmoClip, MaxBulletsChoice ) );
	}

	public override void CreateViewModel()
	{
		base.CreateViewModel();

		ViewModelRenderer?.SetBodyGroup( BulletsBodyGroup, Math.Min( AmmoClip, MaxBulletsChoice ) );
	}
}
