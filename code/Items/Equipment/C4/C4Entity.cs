using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TTT;

public sealed partial class C4Entity : Component, ICarriableHint
{
	public const string BeepSound = "c4_beep-1";
	public const string PlantSound = "c4_plant-1";
	public const string DefuseSound = "c4_defuse-1";
	public const string ExplodeSound = "c4_explode-2";
	public const float MaxTime = 600;
	public const float MinTime = 45;

	public static readonly List<Color> Wires = new()
	{
		Color.Red,
		Color.Yellow,
		Color.Blue,
		Color.White,
		Color.Green,
		Color.FromBytes( 255, 160, 50, 255 ) // Brown
	};

	[Sync] public bool IsArmed { get; private set; }
	[Sync] public TimeUntil TimeUntilExplode { get; private set; }
	[Sync] public Player Planter { get; private set; }

	private RealTimeUntil _nextBeepTime;
	private readonly List<int> _safeWireNumbers = new();

	public void Initialize( Player planter )
	{
		Planter = planter;
		Sound.Play( PlantSound, WorldPosition );
	}

	public static int GetBadWireCount( int timer )
	{
		return Math.Min( (int)MathF.Ceiling( timer / MinTime ), Wires.Count - 1 );
	}

	public void Arm( Player player, int timer )
	{
		if ( IsArmed )
			return;

		var possibleSafeWires = Enumerable.Range( 1, Wires.Count ).ToList();
		possibleSafeWires.Shuffle();

		var safeWireCount = Wires.Count - GetBadWireCount( timer );
		for ( var i = 0; i < safeWireCount; ++i )
			_safeWireNumbers.Add( possibleSafeWires[i] );

		TimeUntilExplode = timer;
		IsArmed = true;

		var note = player.Components.GetOrCreate<C4Note>();
		note.SafeWireNumber = _safeWireNumbers.First();

		BroadcastCloseArmMenu();

		if ( player.Team == Team.Traitors )
			BroadcastC4Marker();
	}

	public void AttemptDefuse( Player defuser, int wire )
	{
		if ( !IsArmed )
			return;

		if ( defuser != Planter && !_safeWireNumbers.Contains( wire ) )
			Explode( true );
		else
			Defuse();
	}

	public void Defuse()
	{
		Sound.Play( DefuseSound, WorldPosition );
		IsArmed = false;
		_safeWireNumbers.Clear();
	}

	private void Explode( bool defusalDetonation = false )
	{
		var radius = 600f;
		if ( defusalDetonation )
			radius /= 2.5f;

		DoDamage( radius );
		Sound.Play( ExplodeSound, WorldPosition );
		GameObject.Destroy();
	}

	private void DoDamage( float radius )
	{
		var isTraitorC4 = Planter?.Team == Team.Traitors;

		foreach ( var player in Utils.GetPlayersWhere( p => p.IsAlive ) )
		{
			var dist = WorldPosition.Distance( player.WorldPosition );
			if ( dist > radius )
				continue;

			var diff = player.WorldPosition - WorldPosition;
			var damage = 125 - MathF.Pow( Math.Max( 0, dist - 490 ), 2 ) * 0.01033057f;

			var damageInfo = new DamageInfo
			{
				Damage = damage,
				Attacker = Planter?.GameObject,
				Position = WorldPosition,
				Force = diff.Normal * damage
			};

			if ( isTraitorC4 && player.Team == Team.Traitors )
				damageInfo.Tags.Add( DamageTags.Avoidable );

			player.TakeDamage( damageInfo );
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost || !IsArmed )
			return;

		if ( _nextBeepTime )
		{
			Sound.Play( BeepSound, WorldPosition );
			_nextBeepTime = Math.Max( TimeUntilExplode, 0.2f );
		}

		if ( TimeUntilExplode )
			Explode();
	}

	void ICarriableHint.Tick( Player player )
	{
		if ( Player.Local != player || !player.IsAlive || !Input.Down( InputAction.Use ) )
		{
			UI.FullScreenHintMenu.Instance?.Close();
			return;
		}

		if ( UI.FullScreenHintMenu.Instance.IsOpen )
			return;

		if ( IsArmed )
			UI.FullScreenHintMenu.Instance?.Open( new UI.C4DefuseMenu( this ) );
		else
			UI.FullScreenHintMenu.Instance?.Open( new UI.C4ArmMenu( this ) );
	}

	Panel ICarriableHint.DisplayHint( Player player ) => new UI.C4Hint( this );

	[ConCmd( "ttt_c4_arm" )]
	public static void ArmC4Cmd( int goId, int time )
	{
		if ( !Networking.IsHost )
			return;

		var player = Utils.GetPlayersWhere( p => p.Network.Owner == Rpc.Caller ).FirstOrDefault();
		if ( player is null )
			return;

		FindById( goId )?.Arm( player, time );
	}

	[ConCmd( "ttt_c4_defuse" )]
	public static void DefuseCmd( int wire, int goId )
	{
		if ( !Networking.IsHost )
			return;

		var player = Utils.GetPlayersWhere( p => p.Network.Owner == Rpc.Caller ).FirstOrDefault();
		if ( player is null )
			return;

		FindById( goId )?.AttemptDefuse( player, wire );
	}

	[ConCmd( "ttt_c4_pickup" )]
	public static void PickupCmd( int goId )
	{
		if ( !Networking.IsHost )
			return;

		var player = Utils.GetPlayersWhere( p => p.Network.Owner == Rpc.Caller ).FirstOrDefault();
		if ( player is null )
			return;

		var c4 = FindById( goId );
		if ( c4 is null )
			return;

		player.Inventory.Add( new C4() );
		c4.GameObject.Destroy();
	}

	[ConCmd( "ttt_c4_delete" )]
	public static void DeleteC4Cmd( int goId )
	{
		if ( !Networking.IsHost )
			return;

		FindById( goId )?.GameObject.Destroy();
	}

	[Rpc.Broadcast]
	private void BroadcastCloseArmMenu()
	{
		if ( UI.FullScreenHintMenu.Instance?.ActivePanel is UI.C4ArmMenu )
			UI.FullScreenHintMenu.Instance.Close();
	}

	[Rpc.Broadcast]
	private void BroadcastC4Marker()
	{
		if ( Player.Local?.Team != Team.Traitors )
			return;

		UI.WorldPoints.Instance?.AddChild(
			new UI.WorldMarker(
				"/ui/c4-icon.png",
				() => TimeSpan.FromSeconds( Math.Max( 0f, (float)TimeUntilExplode ) ).ToString( "mm':'ss" ),
				() => WorldPosition,
				() => !IsValid || !IsArmed
			)
		);
	}

	private static C4Entity FindById( int id )
	{
		return Game.ActiveScene?.GetAllComponents<C4Entity>()
			.FirstOrDefault( c => c.GameObject.Id.GetHashCode() == id );
	}
}

public class C4Note : Component
{
	public int SafeWireNumber { get; set; }
}

