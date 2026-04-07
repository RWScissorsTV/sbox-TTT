using Sandbox;

namespace TTT;

[Title( "Player" ), Icon( "emoji_people" )]
public partial class Player : Component
{
	/// <summary>
	/// The local player instance on this client.
	/// </summary>
	public static Player Local { get; private set; }

	public Inventory Inventory { get; private set; }
	public Perks Perks { get; private set; }
	public ClothingContainer ClothingContainer { get; private set; } = new();

	[RequireComponent] public SkinnedModelRenderer Renderer { get; private set; }
	[RequireComponent] public CharacterController CharController { get; private set; }

	// Input (processed on the owning client)
	public Vector3 InputDirection { get; set; }
	[Sync] public Angles ViewAngles { get; set; }
	public Angles OriginalViewAngles { get; private set; }

	/// <summary>
	/// Eye position in local (body) space.
	/// </summary>
	[Sync] public Vector3 EyeLocalPosition { get; set; }

	/// <summary>
	/// Eye rotation in local (body) space.
	/// </summary>
	[Sync] public Rotation EyeLocalRotation { get; set; }

	/// <summary>
	/// Eye position in world space.
	/// </summary>
	public Vector3 EyePosition => WorldPosition + WorldRotation * EyeLocalPosition;

	/// <summary>
	/// Eye rotation in world space.
	/// </summary>
	public Rotation EyeRotation
	{
		get => WorldRotation * EyeLocalRotation;
		set => EyeLocalRotation = WorldRotation.Inverse * value;
	}

	public Ray AimRay => new( EyePosition, EyeRotation.Forward );

	/// <summary>
	/// The player earns score by killing players on opposite teams, confirming bodies,
	/// or surviving the round.
	/// </summary>
	[Sync] public int Score { get; set; }

	/// <summary>
	/// Score gained during a single round. Added to actual score at round end.
	/// </summary>
	public int RoundScore { get; set; }

	[Sync] public ulong SteamId { get; private set; }
	[Sync] public string SteamName { get; private set; }

	/// <summary>
	/// Gets the network connection that owns this player.
	/// </summary>
	public Connection Client => Network.Owner;

	protected override void OnStart()
	{
		if ( !IsProxy )
		{
			Local = this;
		}

		Tags.Add( "player" );

		Inventory = new( this );
		Perks = new( this );

		Renderer.Model = Model.Load( "models/citizen/citizen.vmdl" );

		Role = new NoneRole();
		Health = 0;
		Status = PlayerStatus.Spectator;

		if ( !IsProxy )
		{
			// Set initial camera
			CameraMode.Current = new FreeCamera();
		}
	}

	/// <summary>
	/// Called on the server when a new connection becomes active (player joins).
	/// </summary>
	public void OnConnectionActive( Connection connection )
	{
		SteamId = connection.SteamId;
		SteamName = connection.DisplayName;
		BaseKarma = Karma.SavedPlayerValues.TryGetValue( connection.SteamId, out var value ) ? value : Karma.StartValue;
		ActiveKarma = BaseKarma;
	}

	public void Respawn()
	{
		if ( !Networking.IsHost )
			return;

		DeleteFlashlight();
		DeleteItems();
		ResetConfirmationData();
		ResetDamageData();
		Role = new NoneRole();

		CharController.Velocity = Vector3.Zero;
		Credits = 0;

		if ( !IsForcedSpectator )
		{
			Health = MaxHealth;
			Status = PlayerStatus.Alive;
			UpdateStatus();

			CharController.Enabled = true;
			Renderer.Enabled = true;

			CreateFlashlight();
			DressPlayer();

			Event.Run( TTTEvent.Player.Spawned, this );
			GameManager.Instance.State.OnPlayerSpawned( this );
		}
		else
		{
			Status = PlayerStatus.Spectator;
			UpdateStatus();
			MakeSpectator();
		}

		BroadcastRespawn();
	}

	[Rpc.Broadcast]
	private void BroadcastRespawn()
	{
		DeleteFlashlight();
		ResetConfirmationData();
		ResetDamageData();

		if ( IsProxy )
		{
			Role = new NoneRole();
		}
		else
		{
			CurrentChannel = IsSpectator ? Channel.Spectator : Channel.All;
			MuteFilter = MuteFilter.None;
			ClearButtons();
		}

		if ( IsSpectator )
			return;

		if ( !IsProxy )
			CameraMode.Current = new FirstPersonCamera();

		CreateFlashlight();

		Event.Run( TTTEvent.Player.Spawned, this );
	}

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			// BuildInput equivalent
			CheckAFK();

			OriginalViewAngles = ViewAngles;
			InputDirection = Input.AnalogMove;

			if ( !Input.StopProcessing )
			{
				var look = Input.AnalogLook;

				if ( ViewAngles.pitch > 90f || ViewAngles.pitch < -90f )
					look = look.WithYaw( look.yaw * -1f );

				var viewAngles = ViewAngles;
				viewAngles += look;
				viewAngles.pitch = viewAngles.pitch.Clamp( -89f, 89f );
				viewAngles.roll = 0f;
				ViewAngles = viewAngles.Normal;

				ActiveCarriable?.BuildInput();
			}

			if ( Input.Pressed( InputAction.Menu ) )
			{
				if ( ActiveCarriable.IsValid() && _lastKnownCarriable.IsValid() )
					(ActiveCarriable, _lastKnownCarriable) = (_lastKnownCarriable, ActiveCarriable);
			}
		}

		// FrameSimulate
		if ( !IsProxy )
		{
			ActiveCarriable?.FrameSimulate();
			DisplayEntityHints();
			ActivateRoleButton();
		}

		SimulateAnimation();
		FrameUpdateFlashlight();
		TickAfkTracking();
	}

	protected override void OnFixedUpdate()
	{
		// Simulate
		if ( !IsProxy )
		{
			if ( ActiveCarriable.IsValid() )
				Inventory.SetActive( ActiveCarriable );

			SimulateActiveCarriable();
			PlayerUse();
		}

		if ( IsAlive )
		{
			if ( !IsProxy )
			{
				SimulateMovement();
				SimulateFlashlight();
				SimulatePerks();
			}
		}

		if ( Networking.IsHost )
		{
			if ( !IsAlive )
			{
				if ( Prop.IsValid() )
					SimulatePossession();

				return;
			}

			CheckLastSeenPlayer();
			CheckPlayerDropCarriable();
		}
	}

	private TimeSince _timeSinceLastFootstep;

	private void OnFootstep( SceneModel.FootstepEvent e )
	{
		if ( !IsAlive )
			return;

		if ( _timeSinceLastFootstep < 0.2f )
			return;

		var volume = FootstepVolume();
		_timeSinceLastFootstep = 0;

		var trace = Scene.Trace.Ray( e.Transform.Position, e.Transform.Position + Vector3.Down * 20 )
			.Radius( 1 )
			.IgnoreGameObject( GameObject )
			.Run();

		if ( !trace.Hit )
			return;

		trace.Surface.DoFootstep( this, trace, e.Foot, volume );
	}

	public float FootstepVolume()
	{
		return CharController.Velocity.WithZ( 0 ).Length.LerpInverse( 0.0f, 200.0f ) * 5.0f;
	}

	private void SimulateAnimation()
	{
		var turnSpeed = 0.02f;
		var rotation = ViewAngles.ToRotation();

		var idealRotation = Rotation.LookAt( rotation.Forward.WithZ( 0 ), Vector3.Up );
		WorldRotation = Rotation.Slerp( WorldRotation, idealRotation, CharController.Velocity.Length * Time.Delta * turnSpeed );
		WorldRotation = WorldRotation.Clamp( idealRotation, 45.0f, out var shuffle );

		Renderer.Set( "wish_velocity", CharController.WishVelocity );
		Renderer.Set( "velocity", CharController.Velocity );
		Renderer.Set( "aim_yaw_offset", 0f );
		Renderer.Set( "b_grounded", CharController.IsOnGround );
		Renderer.Set( "duck_speed", IsDucking ? 1f : 0f );

		// Aim look-at
		var lookAt = EyePosition + EyeRotation.Forward * 100f;
		Renderer.Set( "aim_eyes", lookAt );
		Renderer.Set( "aim_head", lookAt );
		Renderer.Set( "aim_body", lookAt );
		Renderer.Set( "aim_weight", 1.0f );
		Renderer.Set( "aim_body_weight", 1.0f );

		Renderer.Set( "shuffle", shuffle );
		Renderer.Set( "b_sit", false );
		Renderer.Set( "b_noclip", false );

		if ( ActiveCarriable != _lastActiveCarriable )
			Renderer.Set( "b_deploy", true );

		if ( ActiveCarriable is not null )
			ActiveCarriable.SimulateAnimator( Renderer );
		else
		{
			Renderer.Set( "holdtype", (int)CitizenAnimationHelper.HoldTypes.None );
			Renderer.Set( "aim_body_weight", 0.5f );
		}
	}

	public void DeleteItems()
	{
		ClearAmmo();
		Inventory.DeleteContents();
		Perks.DeleteContents();
		ClothingContainer.ClearEntities();
	}

	#region ActiveCarriable
	[Sync] public Carriable ActiveCarriable { get; set; }

	public Carriable _lastActiveCarriable;
	public Carriable _lastKnownCarriable;

	public void SimulateActiveCarriable()
	{
		if ( _lastActiveCarriable != ActiveCarriable )
		{
			OnActiveCarriableChanged( _lastActiveCarriable, ActiveCarriable );
			_lastKnownCarriable = _lastActiveCarriable;
			_lastActiveCarriable = ActiveCarriable;
		}

		if ( !ActiveCarriable.IsValid() )
			return;

		if ( ActiveCarriable.TimeSinceDeployed > ActiveCarriable.Info.DeployTime )
			ActiveCarriable.Simulate();
	}

	public void OnActiveCarriableChanged( Carriable previous, Carriable next )
	{
		previous?.ActiveEnd( this, previous.Owner != this );
		next?.ActiveStart( this );
	}

	/// <summary>
	/// Get the velocity to drop items with.
	/// </summary>
	public Vector3 GetDropVelocity( bool throwUpwards = true )
	{
		return CharController.Velocity + (EyeRotation.Forward + (throwUpwards ? EyeRotation.Up : Vector3.Zero)) * 200;
	}

	private void CheckPlayerDropCarriable()
	{
		if ( Input.Pressed( InputAction.Drop ) && !Input.Down( InputAction.Run ) )
		{
			var droppedCarriable = Inventory.DropActive();
			if ( droppedCarriable is not null && droppedCarriable.Components.TryGet<Rigidbody>( out var rb ) )
				rb.Velocity = GetDropVelocity();
		}
	}
	#endregion

	private void SimulatePerks()
	{
		foreach ( var perk in Perks )
			perk.Simulate();
	}

	protected override void OnDestroy()
	{
		if ( Networking.IsHost )
		{
			Corpse?.GameObject.Destroy();
			Corpse = null;
		}

		DeleteFlashlight();
	}
}

