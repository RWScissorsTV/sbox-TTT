using System.Collections.Generic;
using Sandbox;

namespace TTT;

[Category( "Equipment" )]
[ClassName( "ttt_equipment_teleporter" )]
[Title( "Teleporter" )]
public partial class Teleporter : Carriable
{
	[Sync] public int Charges { get; private set; } = 16;
	[Sync] public bool IsTeleporting { get; private set; }

	public bool LocationIsSet { get; private set; }
	public TimeSince TimeSinceAction { get; private set; }
	public TimeSince TimeSinceStartedTeleporting { get; private set; }

	public override string SlotText => Charges.ToString();
	public override List<UI.BindingPrompt> BindingPrompts => new()
	{
		new( InputAction.PrimaryAttack, LocationIsSet ? "Teleport" : string.Empty ),
		new( InputAction.SecondaryAttack, "Set Teleport Location" ),
	};

	private const float TeleportTime = 4f;
	private bool _hasReachedLocation;
	private Vector3 _teleportLocation;
	private SceneParticles _particle;

	public override void ActiveStart( Player player )
	{
		base.ActiveStart( player );

		IsTeleporting = false;
	}

	public override void ActiveEnd( Player player, bool dropped )
	{
		base.ActiveEnd( player, dropped );

		_particle?.Delete();
		_particle = null;
	}

	public override void Simulate()
	{
		if ( IsTeleporting )
		{
			if ( _particle == null )
			{
				var attachment = Owner.Components.Get<SkinnedModelRenderer>()?.GetAttachment( "spine" );
				if ( attachment.HasValue )
					_particle = SceneParticles.Play( Owner.Scene, "particles/teleporter/teleport.vpcf" );
			}

			if ( TimeSinceStartedTeleporting >= TeleportTime / 2 )
			{
				if ( Networking.IsHost && !_hasReachedLocation )
					Teleport();

				if ( TimeSinceStartedTeleporting >= TeleportTime )
				{
					IsTeleporting = false;
					_particle?.Delete();
					_particle = null;
				}
			}

			return;
		}

		if ( Charges <= 0 || TimeSinceAction < 1f )
			return;

		// Can't teleport unless standing on the ground.
		if ( !Owner.CharController.IsOnGround )
			return;

		if ( Input.Pressed( InputAction.PrimaryAttack ) )
		{
			StartTeleport();
		}
		else if ( Input.Pressed( InputAction.SecondaryAttack ) )
		{
			SetLocation();
		}
	}

	public override void BuildInput()
	{
		base.BuildInput();

		if ( !IsTeleporting )
			return;

		Owner.ActiveCarriable = this;
		Owner.InputDirection = 0;
		Input.Clear( InputAction.Jump );
		Input.Clear( InputAction.Drop );
	}

	private void SetLocation()
	{
		LocationIsSet = true;
		TimeSinceAction = 0;
		_teleportLocation = Owner.WorldPosition;

		if ( !Networking.IsHost )
			UI.InfoFeed.AddEntry( "Teleport location set." );
	}

	private void StartTeleport()
	{
		if ( !LocationIsSet )
			return;

		Charges -= 1;
		IsTeleporting = true;
		TimeSinceAction = 0;
		TimeSinceStartedTeleporting = 0;
		_hasReachedLocation = false;
	}

	private void Teleport()
	{
		_hasReachedLocation = true;
		Owner.WorldPosition = _teleportLocation;

		// TeleFrag players at destination.
		var bbox = BBox.FromPositionAndSize( Owner.WorldPosition, new Vector3( 32f, 32f, 72f ) );

		var damageInfo = new DamageInfo()
			.WithDamage( Player.MaxHealth )
			.WithAttacker( Owner.GameObject )
			.WithTags( DamageTags.Explode, DamageTags.Silent )
			.WithWeapon( GameObject );

		foreach ( var hitPlayer in Utils.GetPlayersWhere( p => p != Owner && p.IsAlive ) )
		{
			if ( bbox.Contains( hitPlayer.WorldPosition ) )
				hitPlayer.TakeDamage( damageInfo );
		}
	}

	protected override void OnDestroy()
	{
		_particle?.Delete();
		_particle = null;
	}
}
