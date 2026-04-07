using Sandbox;
using System;

namespace TTT;

[Category( "Equipment" )]
[ClassName( "ttt_equipment_binoculars" )]
[Title( "Binoculars" )]
public partial class Binoculars : Carriable
{
	private int ZoomLevel { get; set; }

	public bool IsZoomed => ZoomLevel > 0;
	private Corpse _corpse;
	private float _defaultFOV;

	public override void ActiveStart( Player player )
	{
		base.ActiveStart( player );

		_defaultFOV = Game.ActiveScene?.Camera?.FieldOfView ?? 90f;
	}

	public override void ActiveEnd( Player player, bool dropped )
	{
		base.ActiveEnd( player, dropped );

		_corpse = null;
		ZoomLevel = 0;
	}

	public override void Simulate()
	{
		if ( Input.Pressed( InputAction.SecondaryAttack ) )
			ChangeZoomLevel();

		if ( Input.Pressed( InputAction.Reload ) )
		{
			ZoomLevel = 4;
			ChangeZoomLevel();
		}

		if ( !IsZoomed )
			return;

		var trace = Scene.Trace.Ray( Owner.EyePosition, Owner.EyePosition + Owner.EyeRotation.Forward * Player.MaxHintDistance )
			.IgnoreGameObject( GameObject )
			.IgnoreGameObject( Owner.GameObject )
			.WithTag( "interactable" )
			.Run();

		_corpse = trace.GameObject?.Components.Get<Corpse>( FindMode.InSelf );

		if ( !Networking.IsHost || !_corpse.IsValid() )
			return;

		if ( Input.Pressed( InputAction.PrimaryAttack ) )
			_corpse.Search( Owner, Input.Down( InputAction.Run ), false );
	}

	public override void BuildInput()
	{
		base.BuildInput();

		if ( IsZoomed )
			Owner.ViewAngles = Angles.Lerp( Owner.ViewAngles, Owner.ViewAngles, 0.5f / MathF.Pow( 2.5f, ZoomLevel ) );
	}

	protected override void DestroyHudElements()
	{
		base.DestroyHudElements();

		if ( Game.ActiveScene?.Camera is { } cam )
			cam.FieldOfView = _defaultFOV;
	}

	private void ChangeZoomLevel()
	{
		if ( ZoomLevel >= 4 )
		{
			_corpse = null;
			ZoomLevel = 0;

			if ( Game.ActiveScene?.Camera is { } cam )
				cam.FieldOfView = _defaultFOV;

			return;
		}

		if ( !Networking.IsHost )
			Sound.Play( "scope_in", WorldPosition );

		ZoomLevel++;

		if ( Game.ActiveScene?.Camera is { } c )
			c.FieldOfView = 40f / MathF.Pow( 2.5f, ZoomLevel );
	}
}
