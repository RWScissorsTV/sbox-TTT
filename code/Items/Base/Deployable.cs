using Sandbox;
using System;
using System.Collections.Generic;

namespace TTT;

public abstract class Deployable : Carriable
{
	private GhostEntity _ghostEntity;
	private ModelRenderer _ghostRenderer;

	public override List<UI.BindingPrompt> BindingPrompts => new()
	{
		new( InputAction.PrimaryAttack, CanDrop ? "Deploy" : string.Empty ),
		new( InputAction.SecondaryAttack, CanPlant ? "Plant" : string.Empty ),
	};

	protected virtual bool CanDrop => true;
	protected virtual bool CanPlant => true;

	public override void ActiveStart( Player player )
	{
		base.ActiveStart( player );

		WorldRenderer.Enabled = false;

		if ( !CanPlant )
			return;

		var ghostGo = new GameObject( true, "GhostEntity" );
		ghostGo.Components.Create<ModelRenderer>();
		_ghostEntity = ghostGo.Components.Create<GhostEntity>();
		_ghostEntity.SetEntity( WorldRenderer );
		_ghostRenderer = ghostGo.Components.Get<ModelRenderer>( FindMode.InSelf );
	}

	public override void ActiveEnd( Player player, bool dropped )
	{
		base.ActiveEnd( player, dropped );

		_ghostEntity?.GameObject.Destroy();
		_ghostEntity = null;
		_ghostRenderer = null;
	}

	public override void Simulate()
	{
		if ( !Networking.IsHost )
			return;

		if ( CanDrop && Input.Pressed( InputAction.PrimaryAttack ) )
		{
			Owner.Inventory.Drop( this );
			OnDeploy();
			return;
		}

		if ( !CanPlant || !Input.Pressed( InputAction.SecondaryAttack ) )
			return;

		var trace = Scene.Trace.Ray( Owner.EyePosition, Owner.EyePosition + Owner.EyeRotation.Forward * Player.UseDistance )
			.StaticOnly()
			.Run();

		if ( !trace.Hit || _ghostEntity is null )
			return;

		if ( !_ghostEntity.IsPlacementValid( ref trace ) )
			return;

		Owner.Inventory.Drop( this );

		var rb = Components.Get<Rigidbody>( FindMode.InSelf );
		if ( rb is not null )
			rb.Enabled = false;

		GameObject.WorldPosition = trace.EndPosition;
		GameObject.WorldRotation = GetPlacementRotation( trace );

		OnDeploy();
	}

	public override void FrameSimulate()
	{
		base.FrameSimulate();

		if ( _ghostEntity is null || _ghostRenderer is null )
			return;

		var trace = Scene.Trace.Ray( Owner.EyePosition, Owner.EyePosition + Owner.EyeRotation.Forward * Player.UseDistance )
			.StaticOnly()
			.Run();

		if ( !trace.Hit )
		{
			_ghostRenderer.Enabled = false;
			return;
		}

		_ghostRenderer.Enabled = true;
		_ghostEntity.GameObject.WorldPosition = trace.EndPosition;
		_ghostEntity.GameObject.WorldRotation = GetPlacementRotation( trace );

		if ( _ghostEntity.IsPlacementValid( ref trace ) )
			_ghostEntity.ShowValid();
		else
			_ghostEntity.ShowInvalid();
	}

	private static Rotation GetPlacementRotation( SceneTraceResult trace )
	{
		var rot = Rotation.From( trace.Normal.EulerAngles );

		if ( Math.Abs( trace.Normal.z ) >= 0.99f )
		{
			rot = Rotation.From(
				rot.Angles()
				.WithYaw( 0f )
				.WithPitch( -90 * trace.Normal.z.CeilToInt() )
				.WithRoll( 180f )
			);
		}

		return rot;
	}

	protected virtual void OnDeploy() { }
}
