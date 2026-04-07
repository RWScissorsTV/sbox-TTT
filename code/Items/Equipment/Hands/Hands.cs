using System.Collections.Generic;
using Sandbox;

namespace TTT;

public interface IGrabbable
{
	string PrimaryAttackHint { get; }
	string SecondaryAttackHint { get; }
	bool IsHolding { get; }
	GameObject Drop();
	void Update( Player player );
	void SecondaryAction();
}

[ClassName( "ttt_equipment_hands" )]
[HideInEditor]
[Title( "Hands" )]
public partial class Hands : Carriable
{
	[Sync] private string CurrentPrimaryHint { get; set; }
	[Sync] private string CurrentSecondaryHint { get; set; }

	public override List<UI.BindingPrompt> BindingPrompts => new()
	{
		new( InputAction.PrimaryAttack, !CurrentPrimaryHint.IsNullOrEmpty() ? CurrentPrimaryHint : "Pickup" ),
		new( InputAction.SecondaryAttack, !CurrentSecondaryHint.IsNullOrEmpty() ? CurrentSecondaryHint : "Push" ),
	};

	public GameObject GrabPoint { get; private set; }
	public const string MiddleHandsAttachment = "middle_of_both_hands";

	private bool IsHoldingEntity => _grabbedEntity is not null && _grabbedEntity.IsHolding;
	private bool IsPushing { get; set; } = false;
	private IGrabbable _grabbedEntity;

	private const float MaxPickupMass = 205;
	private const float PushForce = 350f;
	private readonly Vector3 _maxPickupSize = new( 26, 22, 50 );

	public override void Simulate()
	{
		if ( !Networking.IsHost )
			return;

		if ( Input.Pressed( InputAction.PrimaryAttack ) )
		{
			if ( IsHoldingEntity )
				_grabbedEntity.SecondaryAction();
			else
				TryGrabEntity();
		}
		else if ( Input.Pressed( InputAction.SecondaryAttack ) )
		{
			if ( IsHoldingEntity )
			{
				_grabbedEntity.Drop();
				_grabbedEntity = null;
			}
			else
				PushEntity();
		}

		if ( _grabbedEntity is null )
			return;

		_grabbedEntity.Update( Owner );

		if ( !_grabbedEntity.IsHolding )
		{
			_grabbedEntity = null;
			CurrentPrimaryHint = null;
			CurrentSecondaryHint = null;
			return;
		}

		CurrentPrimaryHint = _grabbedEntity.PrimaryAttackHint;
		CurrentSecondaryHint = _grabbedEntity.SecondaryAttackHint;
	}

	private void PushEntity()
	{
		if ( IsPushing )
			return;

		var trace = Scene.Trace.Ray( Owner.EyePosition, Owner.EyePosition + Owner.EyeRotation.Forward * Player.UseDistance )
			.IgnoreGameObject( Owner.GameObject )
			.Run();

		if ( !trace.Hit || trace.GameObject is null )
			return;

		var rb = trace.GameObject.Components.Get<Rigidbody>();
		if ( rb?.PhysicsBody is not null )
			rb.PhysicsBody.Velocity += Owner.EyeRotation.Forward * PushForce;

		IsPushing = true;

		Owner.SetAnimParameter( "b_attack", true );
		Owner.SetAnimParameter( "holdtype", 4 );
		Owner.SetAnimParameter( "holdtype_handedness", 0 );

		Utils.DelayAction( 0.5f, () => IsPushing = false );
	}

	private void TryGrabEntity()
	{
		var eyePos = Owner.EyePosition;
		var eyeDir = Owner.EyeRotation.Forward;

		var trace = Scene.Trace.Ray( eyePos, eyePos + eyeDir * Player.UseDistance )
			.UseHitboxes()
			.IgnoreGameObject( Owner.GameObject )
			.WithAnyTags( "solid", "interactable" )
			.Run();

		if ( !trace.Hit || trace.GameObject is null || trace.Body is null )
			return;

		if ( trace.Body.BodyType != PhysicsBodyType.Dynamic )
			return;

		if ( trace.GameObject.Components.TryGet<Player>( out _ ) )
			return;

		// Cannot pickup items held by other players.
		if ( trace.GameObject.Parent.IsValid() )
			return;

		if ( trace.GameObject.Components.TryGet<Corpse>( out var corpse ) )
		{
			_grabbedEntity = new GrabbableCorpse( Owner, corpse );
		}
		else if ( trace.GameObject.Components.TryGet<Carriable>( out _ ) )
		{
			_grabbedEntity = new GrabbableProp( Owner, trace.GameObject );
		}
		else
		{
			if ( CanPickup( trace.GameObject ) )
				_grabbedEntity = new GrabbableProp( Owner, trace.GameObject );
		}
	}

	public override void OnCarryStart( Player player )
	{
		base.OnCarryStart( player );

		if ( !Networking.IsHost )
			return;

		GrabPoint = new GameObject( true, "GrabPoint" );
		var renderer = GrabPoint.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( "models/hands/grabpoint.vmdl" );
		GrabPoint.Parent = player.GameObject;
	}

	public override void OnCarryDrop( Player player )
	{
		base.OnCarryDrop( player );

		if ( !Networking.IsHost )
			return;

		_grabbedEntity?.Drop();
		GrabPoint?.Destroy();
		GrabPoint = null;
	}

	public override void ActiveEnd( Player player, bool dropped )
	{
		_grabbedEntity?.Drop();
		_grabbedEntity = null;

		base.ActiveEnd( player, dropped );
	}

	public override void SimulateAnimator( SkinnedModelRenderer renderer )
	{
		if ( IsHoldingEntity || IsPushing )
		{
			renderer.Set( "holdtype", 4 ); // HoldItem
			renderer.Set( "holdtype_handedness", 0 );
		}
		else
		{
			renderer.Set( "holdtype", 0 ); // None
		}
	}

	private bool CanPickup( GameObject go )
	{
		var rb = go.Components.Get<Rigidbody>();
		if ( rb?.PhysicsBody is null )
			return false;

		if ( rb.PhysicsBody.Mass > MaxPickupMass )
			return false;

		var modelRenderer = go.Components.Get<ModelRenderer>();
		if ( modelRenderer is null )
			return false;

		var size = modelRenderer.LocalBounds.Size;
		return size.x < _maxPickupSize.x && size.y < _maxPickupSize.y && size.z < _maxPickupSize.z;
	}

	[TTTEvent.Round.End]
	private void OnRoundEnd( Team winningTeam, WinType winType )
	{
		_grabbedEntity?.Drop();
		_grabbedEntity = null;
	}
}
