using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TTT;

public enum FireMode
{
	Automatic = 0,
	Semi = 1,
	Burst = 2
}

[Title( "Weapon" ), Icon( "sports_martial_arts" )]
public abstract partial class Weapon : Carriable
{
	[Sync] public int AmmoClip { get; protected set; }
	[Sync] public int ReserveAmmo { get; protected set; }
	[Sync] public bool IsReloading { get; protected set; }
	[Sync] public TimeSince TimeSincePrimaryAttack { get; protected set; }
	[Sync] public TimeSince TimeSinceSecondaryAttack { get; protected set; }
	[Sync] public TimeSince TimeSinceReload { get; protected set; }

	public new WeaponInfo Info => (WeaponInfo)base.Info;
	public override string SlotText => $"{AmmoClip} + {ReserveAmmo + Owner?.AmmoCount( Info.AmmoType )}";
	private Vector2 RecoilOnShoot => new( Game.Random.Float( -Info.HorizontalRecoilRange, Info.HorizontalRecoilRange ), Info.VerticalRecoil );
	private Vector2 CurrentRecoil { get; set; } = Vector2.Zero;

	protected override void OnStart()
	{
		base.OnStart();

		AmmoClip = Info?.ClipSize ?? 0;

		if ( Info?.AmmoType == AmmoType.None )
			ReserveAmmo = Info.ReserveAmmo;
	}

	public override void ActiveStart( Player player )
	{
		base.ActiveStart( player );

		IsReloading = false;
		TimeSinceReload = 0;
	}

	public override void Simulate()
	{
		if ( CanReload() )
		{
			Reload();
			return;
		}

		if ( !IsReloading )
		{
			if ( CanPrimaryAttack() )
			{
				TimeSincePrimaryAttack = 0;
				AttackPrimary();
			}

			if ( CanSecondaryAttack() )
			{
				TimeSinceSecondaryAttack = 0;
				AttackSecondary();
			}

			if ( Input.Down( InputAction.Run ) && Input.Pressed( InputAction.Drop ) )
				DropAmmo();
		}
		else if ( TimeSinceReload > Info.ReloadTime )
		{
			OnReloadFinish();
		}
	}

	public override void BuildInput()
	{
		base.BuildInput();

		if ( Owner is null )
			return;

		var oldPitch = Owner.ViewAngles.pitch;
		var oldYaw = Owner.ViewAngles.yaw;

		var recoil = Owner.ViewAngles;
		recoil.pitch -= CurrentRecoil.y * Time.Delta;
		recoil.yaw -= CurrentRecoil.x * Time.Delta;

		Owner.ViewAngles = recoil;

		CurrentRecoil -= CurrentRecoil
			.WithY( (oldPitch - Owner.ViewAngles.pitch) * Info.RecoilRecoveryScale )
			.WithX( (oldYaw - Owner.ViewAngles.yaw) * Info.RecoilRecoveryScale );
	}

	protected virtual bool CanPrimaryAttack()
	{
		if ( Info.FireMode == FireMode.Semi && !Input.Pressed( InputAction.PrimaryAttack ) )
			return false;
		else if ( Info.FireMode != FireMode.Semi && !Input.Down( InputAction.PrimaryAttack ) )
			return false;

		var rate = Info.PrimaryRate;
		if ( rate <= 0 )
			return true;

		return TimeSincePrimaryAttack > (1 / rate);
	}

	protected virtual bool CanSecondaryAttack()
	{
		if ( !Input.Pressed( InputAction.SecondaryAttack ) )
			return false;

		var rate = Info.SecondaryRate;
		if ( rate <= 0 )
			return true;

		return TimeSinceSecondaryAttack > (1 / rate);
	}

	protected virtual void AttackPrimary()
	{
		if ( AmmoClip == 0 )
		{
			BroadcastDryFireEffects();
			Sound.Play( Info.DryFireSound, WorldPosition );
			return;
		}

		AmmoClip--;

		Owner.Renderer.Set( "b_attack", true );
		BroadcastShootEffects();
		Sound.Play( Info.FireSound, WorldPosition );

		ShootBullet( Info.Spread, 1.5f, Info.Damage, 2.0f, Info.BulletsPerFire );
	}

	protected virtual void AttackSecondary() { }

	protected virtual bool CanReload()
	{
		if ( IsReloading )
			return false;

		if ( !Input.Pressed( InputAction.Reload ) )
			return false;

		if ( AmmoClip >= Info.ClipSize || (Owner.AmmoCount( Info.AmmoType ) <= 0 && ReserveAmmo <= 0) )
			return false;

		return true;
	}

	protected virtual void Reload()
	{
		if ( IsReloading )
			return;

		TimeSinceReload = 0;
		IsReloading = true;

		Owner.Renderer.Set( "b_reload", true );
		BroadcastReloadEffects();
	}

	protected virtual void OnReloadFinish()
	{
		IsReloading = false;
		AmmoClip += TakeAmmo( Info.ClipSize - AmmoClip );
	}

	[Rpc.Broadcast]
	protected virtual void BroadcastShootEffects()
	{
		if ( !Info.MuzzleFlashParticle.IsNullOrEmpty() )
		{
			var attachment = WorldRenderer.GetAttachment( "muzzle" );
			if ( attachment.HasValue )
				SceneParticles.PlayInstant( Scene, Info.MuzzleFlashParticle, attachment.Value );
		}

		ViewModelRenderer?.Set( "fire", true );
		CurrentRecoil += RecoilOnShoot;
	}

	[Rpc.Broadcast]
	protected virtual void BroadcastDryFireEffects()
	{
		ViewModelRenderer?.Set( "dryfire", true );
	}

	[Rpc.Broadcast]
	protected virtual void BroadcastReloadEffects()
	{
		ViewModelRenderer?.Set( "reload", true );
	}

	protected virtual void ShootBullet( float spread, float force, float damage, float bulletSize, int bulletCount )
	{
		Game.SetRandomSeed( Time.Tick );

		while ( bulletCount-- > 0 )
		{
			var forward = Owner.EyeRotation.Forward;
			forward += (Vector3.Random + Vector3.Random + Vector3.Random + Vector3.Random) * spread * 0.25f;
			forward = forward.Normal;

			foreach ( var trace in TraceBullet( Owner.EyePosition, Owner.EyePosition + forward * 20000f, bulletSize ) )
			{
				trace.Surface.DoBulletImpact( trace );

				var fullEndPosition = trace.EndPosition + trace.Direction * bulletSize;

				if ( !Info.TracerParticle.IsNullOrEmpty() && trace.Distance > 200 )
				{
					var tracer = new SceneParticles( Scene.SceneWorld, Info.TracerParticle );
					if ( tracer is not null )
					{
						tracer.SetControlPoint( 0, EffectTransform.Position );
						tracer.SetControlPoint( 1, trace.EndPosition );
					}
				}

				if ( !Networking.IsHost )
					continue;

				var hitPlayer = trace.GameObject?.Components.TryGet<Player>( out var hitP ) == true ? hitP : null;
				var hitCarriable = trace.GameObject?.Components.TryGet<Carriable>( out var hitC ) == true ? hitC : null;

				if ( hitPlayer is null && hitCarriable is null && trace.GameObject is null )
					continue;

				OnHit( trace );

				if ( Info.Damage <= 0 )
					continue;

				if ( hitPlayer is not null )
				{
					var dmgInfo = DamageInfo.FromBullet( trace.EndPosition, forward * 100f * force, damage )
						.UsingTraceResult( trace )
						.WithAttacker( Owner.GameObject )
						.WithWeapon( this );

					if ( Info.IsSilenced || dmgInfo.IsHeadshot() )
						dmgInfo = dmgInfo.WithTag( "silent" );

					hitPlayer.TakeDamage( dmgInfo );
				}
			}
		}
	}

	/// <summary>
	/// Called when the bullet hits something.
	/// </summary>
	protected virtual void OnHit( SceneTraceResult trace ) { }

	/// <summary>
	/// Trace from start to end, returns all hits (for glass penetration, etc.)
	/// </summary>
	protected IEnumerable<SceneTraceResult> TraceBullet( Vector3 start, Vector3 end, float radius = 2.0f )
	{
		var results = new List<SceneTraceResult>();

		var trace = Scene.Trace.Ray( start, end )
			.UseHitboxes()
			.WithAnyTags( "solid", "player", "glass", "interactable" )
			.IgnoreGameObject( GameObject )
			.Size( radius );

		var tr = trace.Run();

		if ( tr.Hit )
			results.Add( tr );

		// Shoot through glass
		bool hitGlass = tr.Tags?.Contains( "glass" ) == true;
		if ( hitGlass )
		{
			var tr2 = Scene.Trace.Ray( tr.EndPosition, end )
				.UseHitboxes()
				.WithAnyTags( "solid", "player", "glass", "interactable" )
				.IgnoreGameObject( tr.GameObject )
				.Size( radius )
				.Run();

			if ( tr2.Hit )
				results.Add( tr2 );
		}

		return results;
	}

	protected void DropAmmo()
	{
		if ( Info.AmmoType == AmmoType.None || AmmoClip <= 0 )
			return;

		if ( Networking.IsHost )
			Ammo.Drop( Owner, Info.AmmoType, AmmoClip );

		AmmoClip = 0;
	}

	protected int TakeAmmo( int ammo )
	{
		var available = Math.Min( Info.AmmoType == AmmoType.None ? ReserveAmmo : Owner.AmmoCount( Info.AmmoType ), ammo );

		if ( Info.AmmoType == AmmoType.None )
			ReserveAmmo -= available;
		else
			Owner.TakeAmmo( Info.AmmoType, available );

		return available;
	}

	public static float GetDamageFalloff( float distance, float damage, float start, float end )
	{
		if ( end > 0f )
		{
			if ( start > 0f )
			{
				if ( distance < start )
					return damage;

				var falloffRange = end - start;
				var difference = (distance - start);

				return Math.Max( damage - (damage / falloffRange) * difference, 0f );
			}

			return Math.Max( damage - (damage / end) * distance, 0f );
		}

		return damage;
	}
}
