using Editor;
using Sandbox;

namespace TTT;

[Category( "Weapons" )]
[ClassName( "ttt_weapon_knife" )]
[Title( "Knife" )]
public partial class Knife : Carriable
{
	[ConVar( "ttt_knife_backstabs" )]
	public static bool Backstabs { get; set; } = true;

	public TimeSince TimeSinceStab { get; private set; }

	private const string SwingSound = "knife_swing-1";
	private const string FleshHit = "knife_flesh_hit-1";

	private bool _isThrown = false;
	private Player _thrower;
	private Vector3 _thrownFrom;
	private Rotation _throwRotation = Rotation.From( new Angles( 90, 0, 0 ) );
	private float _gravityModifier;

	public override void Simulate()
	{
		if ( TimeSinceStab < 1f )
			return;

		if ( Input.Down( InputAction.PrimaryAttack ) )
		{
			TimeSinceStab = 0;
			StabAttack( 35f, 8f );
		}
		else if ( Input.Released( InputAction.SecondaryAttack ) )
		{
			Throw();
		}
	}

	public override bool CanCarry( Player carrier )
	{
		return !_isThrown && base.CanCarry( carrier );
	}

	private void StabAttack( float range, float radius )
	{
		Owner.Renderer?.Set( "b_attack", true );
		BroadcastSwingEffects();
		Sound.Play( SwingSound, WorldPosition );

		var endPosition = Owner.EyePosition + Owner.EyeRotation.Forward * range;

		var trace = Scene.Trace.Ray( Owner.EyePosition, endPosition )
			.IgnoreGameObject( Owner.GameObject )
			.Size( radius )
			.UseHitboxes()
			.Run();

		if ( !trace.Hit )
			return;

		trace.Surface?.DoBulletImpact( trace );

		if ( !Networking.IsHost )
			return;

		Player hitPlayer = null;
		trace.GameObject?.Components.TryGet( out hitPlayer, FindMode.InAncestors );

		var damageInfo = new DamageInfo()
			.WithDamage( Backstabs ? 50 : 100 )
			.UsingTraceResult( trace )
			.WithAttacker( Owner.GameObject )
			.WithTags( DamageTags.Slash, DamageTags.Silent )
			.WithWeapon( GameObject );

		if ( hitPlayer is not null )
		{
			Sound.Play( FleshHit, WorldPosition );

			if ( Backstabs && IsBehindAndFacingTarget( hitPlayer ) )
				damageInfo = damageInfo.WithDamage( damageInfo.Damage * 2 );

			hitPlayer.TakeDamage( damageInfo );

			if ( !hitPlayer.IsAlive )
			{
				Owner.Inventory.DropActive();
				GameObject.Destroy();
			}
		}
	}

	private bool IsBehindAndFacingTarget( Player target )
	{
		var toOwner = new Vector2( Owner.WorldPosition - target.WorldPosition ).Normal;
		var ownerForward = new Vector2( Owner.EyeRotation.Forward ).Normal;
		var targetForward = new Vector2( target.EyeRotation.Forward ).Normal;

		var behindDot = Vector2.Dot( toOwner, targetForward );
		var facingDot = Vector2.Dot( toOwner, ownerForward );
		var viewDot = Vector2.Dot( targetForward, ownerForward );

		return behindDot < 0.0f && facingDot < -0.5f && viewDot > -0.3f;
	}

	private void Throw()
	{
		_thrower = Owner;
		_isThrown = true;
		_thrownFrom = Owner.WorldPosition;
		_gravityModifier = 0;

		if ( !Networking.IsHost )
			return;

		if ( IsActive )
			Owner.Inventory.DropActive();
		else
			Owner.Inventory.Drop( this );

		// Disable physics collider while thrown (manual movement)
		var rb = Components.Get<Rigidbody>( FindMode.InSelf );
		if ( rb is not null )
			rb.Enabled = false;

		WorldPosition = Owner.EyePosition;
		WorldRotation = _thrower.EyeRotation * _throwRotation;
	}

	[Rpc.Broadcast]
	protected void BroadcastSwingEffects()
	{
		ViewModelRenderer?.Set( "fire", true );
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost || !_isThrown )
			return;

		var oldPosition = WorldPosition;
		var newPosition = WorldPosition;
		newPosition += (WorldRotation.Forward * 600f) * Time.Delta;

		_gravityModifier += 8;
		newPosition -= new Vector3( 0f, 0f, _gravityModifier * Time.Delta );

		var trace = Scene.Trace.Ray( WorldPosition, newPosition )
			.UseHitboxes()
			.WithAnyTags( "solid" )
			.IgnoreGameObject( _thrower.IsValid() ? _thrower.GameObject : null )
			.IgnoreGameObject( GameObject )
			.Run();

		WorldPosition = trace.EndPosition;
		WorldRotation = Rotation.From( trace.Direction.EulerAngles ) * _throwRotation;

		if ( !trace.Hit )
			return;

		Player hitPlayer = null;
		trace.GameObject?.Components.TryGet( out hitPlayer, FindMode.InAncestors );

		if ( hitPlayer is not null )
		{
			trace.Surface?.DoBulletImpact( trace );

			var damageInfo = new DamageInfo()
				.WithDamage( 100f )
				.UsingTraceResult( trace )
				.WithAttacker( _thrower?.IsValid() == true ? _thrower.GameObject : null )
				.WithTags( DamageTags.Slash, DamageTags.Silent )
				.WithWeapon( GameObject );

			hitPlayer.TakeDamage( damageInfo );

			GameObject.Destroy();
		}
		else if ( trace.Body?.IsStatic == true )
		{
			// Check angle to see if knife sticks or bounces
			if ( Vector3.GetAngle( trace.Normal, trace.Direction ) < 120 )
			{
				// Bounce
				WorldPosition = oldPosition - trace.Direction * 5;
				var rb = Components.Get<Rigidbody>( FindMode.InSelf );
				if ( rb is not null )
				{
					rb.Enabled = true;
					var mass = rb.PhysicsBody?.Mass ?? 1f;
					rb.Velocity = trace.Direction * 500f * mass;
				}
				_isThrown = false;
			}
			else
			{
				// Stick in wall
				trace.Surface?.DoBulletImpact( trace );
				WorldPosition -= trace.Direction * 4f;
				_isThrown = false;
			}
		}
		else
		{
			// Hit something else — bounce
			WorldPosition = oldPosition - trace.Direction * 5;
			var rb = Components.Get<Rigidbody>( FindMode.InSelf );
			if ( rb is not null )
			{
				rb.Enabled = true;
				var mass = rb.PhysicsBody?.Mass ?? 1f;
				rb.Velocity = trace.Direction * 500f * mass;
			}
			_isThrown = false;
		}
	}
}
