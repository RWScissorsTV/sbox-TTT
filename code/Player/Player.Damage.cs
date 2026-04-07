using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TTT;

public partial class Player
{
	public const float MaxHealth = 100f;

	[Sync] public TimeSince TimeSinceDeath { get; private set; }

	/// <summary>
	/// Better to use this than LastAttackerWeapon because the weapon may be invalid.
	/// </summary>
	public CarriableInfo LastAttackerWeaponInfo { get; private set; }

	public DamageInfo LastDamage { get; private set; }

	private float _health;
	public float Health
	{
		get => _health;
		set => _health = Math.Clamp( value, 0, MaxHealth );
	}

	public GameObject LastAttacker { get; set; }
	public Component LastAttackerWeapon { get; set; }

	/// <summary>
	/// Whether killed by another Player (including via props).
	/// </summary>
	public bool KilledByPlayer => LastAttacker?.Components.TryGet<Player>( out var p ) == true && p != this;

	[Sync] public float BaseKarma { get; set; }
	[Sync] public float DamageFactor { get; set; } = 1f;
	[Sync] public float KarmaSpeedScale { get; set; } = 1f;
	[Sync] public float RdmDamageScale { get; set; } = 1f;
	public TimeUntil TimeUntilClean { get; set; } = 0f;
	public float ActiveKarma { get; set; }

	public void Kill()
	{
		TakeDamage( DamageInfo.Generic( float.MaxValue ).WithAttacker( this ) );
	}

	public void OnKilled()
	{
		if ( !Networking.IsHost )
			return;

		Status = PlayerStatus.MissingInAction;
		TimeSinceDeath = 0;

		if ( KilledByPlayer && LastAttacker?.Components.TryGet<Player>( out var attacker ) == true )
			attacker.PlayersKilled.Add( this );

		Corpse = new Corpse( this );
		StopUsing();

		CharController.Enabled = false;
		Renderer.Enabled = false;

		Inventory.DropAll();
		DeleteFlashlight();
		DeleteItems();

		if ( !LastDamage.IsSilent() )
			Sound.Play( "player_death", WorldPosition );

		Event.Run( TTTEvent.Player.Killed, this );
		GameManager.Instance.State.OnPlayerKilled( this );

		BroadcastOnKilled();
	}

	[Rpc.Broadcast]
	private void BroadcastOnKilled()
	{
		if ( !IsProxy )
		{
			CurrentChannel = Channel.Spectator;

			if ( Corpse.IsValid() )
				CameraMode.Current = new FollowEntityCamera( Corpse.GameObject );

			ClearButtons();
		}

		DeleteFlashlight();
		Event.Run( TTTEvent.Player.Killed, this );
	}

	public void TakeDamage( DamageInfo info )
	{
		if ( !Networking.IsHost )
			return;

		if ( !IsAlive )
			return;

		if ( info.Attacker is GameObject attGo && attGo.Tags.Has( DamageTags.IgnoreDamage ) )
			return;

		if ( info.Attacker?.Components.TryGet<Player>( out var attacker ) == true && attacker != this )
		{
			if ( GameManager.Instance.State is not InProgress and not PostRound )
				return;

			info = info.WithDamage( info.Damage * attacker.RdmDamageScale );

			if ( !info.HasTag( DamageTags.Slash ) )
				info = info.WithDamage( info.Damage * attacker.DamageFactor );
		}

		if ( info.HasTag( DamageTags.Bullet ) )
		{
			info = info.WithDamage( info.Damage * GetBulletDamageMultipliers( info ) );
			CreateBloodSplatter( info, 180f );
		}

		if ( info.HasTag( DamageTags.Slash ) )
			CreateBloodSplatter( info, 64f );

		if ( info.HasTag( DamageTags.Blast ) )
			BroadcastDeafen( info.Damage.LerpInverse( 0, 60 ) );

		info = info.WithDamage( Math.Min( Health, info.Damage ) );

		LastAttacker = info.Attacker;
		LastAttackerWeapon = info.Weapon;
		LastAttackerWeaponInfo = (info.Weapon as Carriable)?.Info;
		LastDamage = info;

		Health -= info.Damage;
		Event.Run( TTTEvent.Player.TookDamage, this );

		BroadcastDamageInfo( info );

		if ( Health <= 0f )
			OnKilled();
	}

	private void CreateBloodSplatter( DamageInfo info, float maxDistance )
	{
		var trace = Scene.Trace.Ray( new Ray( info.Position, info.Force.Normal ), maxDistance )
			.IgnoreGameObject( GameObject )
			.Run();

		if ( !trace.Hit )
			return;

		var decal = ResourceLibrary.Get<DecalDefinition>( "decals/blood_splatter.decal" );
		Decal.Place( decal, trace.EndPosition - trace.Direction * 1f, Rotation.LookAt( trace.Normal ), Color.White );
	}

	private float GetBulletDamageMultipliers( DamageInfo info )
	{
		var damageMultiplier = 1f;

		if ( Perks.Has<Armor>() )
			damageMultiplier *= Armor.ReductionPercentage;

		if ( info.IsHeadshot() )
		{
			GameObject weaponGo = info.Weapon?.GameObject;
			var carriable = weaponGo?.Components.Get<Carriable>();
			if ( carriable?.Info is WeaponInfo wInfo )
				damageMultiplier *= wInfo.HeadshotMultiplier;
			else
				damageMultiplier *= 2f;
		}
		else if ( info.HasTag( "arm" ) || info.HasTag( "hand" ) )
		{
			damageMultiplier *= 0.55f;
		}

		return damageMultiplier;
	}

	private void ResetDamageData()
	{
		LastAttacker = null;
		LastAttackerWeapon = null;
		LastAttackerWeaponInfo = null;
		LastDamage = default;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void BroadcastDeafen( float strength )
	{
		Audio.SetEffect( "flashbang", strength, velocity: 20.0f, fadeOut: 4.0f * strength );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void BroadcastDamageInfo( DamageInfo info )
	{
		LastAttacker = info.Attacker;
		LastAttackerWeapon = info.Weapon;
		LastDamage = info;

		if ( !IsProxy )
			Event.Run( TTTEvent.Player.TookDamage, this );
	}
}

