using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using static TTT.TTTEvent;

namespace TTT;

[Category( "Equipment" )]
[ClassName( "ttt_equipment_dnascanner" )]
[HideInEditor]
[Title( "DNA Scanner" )]
public partial class DNAScanner : Carriable
{
	// DNA samples collected by this scanner (server-authoritative, owner-only view)
	public List<DNA> DNACollected { get; private set; } = new();

	[Sync] public int? SelectedId { get; set; }
	[Sync] public bool AutoScan { get; set; } = false;
	[Sync] private float Charge { get; set; } = MaxCharge;

	public override List<UI.BindingPrompt> BindingPrompts => new()
	{
		new( InputAction.PrimaryAttack, "Fetch DNA" ),
		new( InputAction.SecondaryAttack, !AutoScan ? "Scan" : string.Empty ),
		new( InputAction.View, "DNA Menu" )
	};

	public override string SlotText => $"{(int)Charge}%";
	public bool IsCharging => Charge < MaxCharge;

	private const float MaxCharge = 100f;
	private const float ChargePerSecond = 2.2f;
	private UI.WorldMarker _marker;

	public override void Simulate()
	{
		if ( Input.Pressed( InputAction.PrimaryAttack ) )
			FetchDNA();

		if ( Input.Pressed( InputAction.SecondaryAttack ) )
			Scan();
	}

	public override void OnCarryDrop( Player dropper )
	{
		base.OnCarryDrop( dropper );

		if ( Player.Local != dropper )
			return;

		_marker?.Delete( true );
	}

	public void Scan()
	{
		if ( !Networking.IsHost || IsCharging )
			return;

		var selectedDNA = FindSelectedDNA( SelectedId );
		if ( selectedDNA is null )
			return;

		var target = selectedDNA.GetTarget();
		if ( target is null || !target.IsValid() )
		{
			RemoveDNA( selectedDNA );
			BroadcastInfoEntry( Owner.Network.Owner, "DNA not detected in area." );
			return;
		}

		var dist = Owner.WorldPosition.Distance( target.WorldPosition );
		Charge = Math.Max( 0, Charge - Math.Max( 4, dist / 25f ) );
		BroadcastUpdateMarker( Owner.Network.Owner, target.WorldPosition );
	}

	public void RemoveDNA( DNA dna )
	{
		if ( dna.Id == SelectedId )
		{
			SelectedId = null;
			BroadcastDeleteMarker( Owner.Network.Owner );
		}

		DNACollected.Remove( dna );
	}

	private void FetchDNA()
	{
		if ( !Networking.IsHost )
			return;

		var trace = Scene.Trace.Ray( Owner.EyePosition, Owner.EyePosition + Owner.EyeRotation.Forward * Player.UseDistance )
			.IgnoreGameObject( GameObject )
			.IgnoreGameObject( Owner.GameObject )
			.WithTag( "interactable" )
			.Run();

		if ( !trace.Hit || trace.GameObject is null )
			return;

		if ( trace.GameObject.Components.TryGet<Corpse>( out var corpse ) && !corpse.Player.IsConfirmedDead )
		{
			BroadcastInfoEntry( Owner.Network.Owner, "Corpse must be identified to retrieve DNA sample." );
			return;
		}

		var samples = trace.GameObject.Components.GetAll<DNA>();
		if ( !samples.Any() )
			return;

		var totalCollected = 0;
		foreach ( var dna in samples )
		{
			if ( dna.TimeUntilDecayed )
			{
				dna.Enabled = false;
				continue;
			}

			if ( !DNACollected.Contains( dna ) )
			{
				DNACollected.Add( dna );
				totalCollected += 1;
			}
		}

		if ( totalCollected > 0 )
			BroadcastInfoEntry( Owner.Network.Owner, $"Collected {totalCollected} new DNA sample(s)." );
	}

	private DNA FindSelectedDNA( int? id )
	{
		if ( id is null )
			return null;

		foreach ( var sample in DNACollected )
			if ( sample.Id == id )
				return sample;

		return null;
	}

	[Event.Tick]
	private void OnTick()
	{
		if ( !Networking.IsHost || Owner is null )
			return;

		Charge = Math.Min( Charge + ChargePerSecond * Time.Delta, MaxCharge );

		if ( AutoScan )
			Scan();
	}

	[Rpc.Broadcast]
	private static void BroadcastUpdateMarker( Connection to, Vector3 pos )
	{
		if ( Connection.Local != to )
			return;

		Sound.Play( "dna-beep", pos );

		var player = Player.Local;
		if ( player is null )
			return;

		var scanner = player.Inventory.Find<DNAScanner>();
		if ( scanner is null )
			return;

		scanner._marker?.Delete( true );
		scanner._marker = new UI.WorldMarker(
			"/ui/dna-icon.png",
			() => $"{Player.Local.WorldPosition.Distance( pos ).SourceUnitsToMeters():n0}m",
			() => pos
		);
		UI.WorldPoints.Instance?.AddChild( scanner._marker );
	}

	[Rpc.Broadcast]
	private static void BroadcastDeleteMarker( Connection to )
	{
		if ( Connection.Local != to )
			return;

		var scanner = Player.Local?.Inventory.Find<DNAScanner>();
		scanner?._marker?.Delete( true );
	}

	[Rpc.Broadcast]
	private static void BroadcastInfoEntry( Connection to, string message )
	{
		if ( Connection.Local != to )
			return;

		UI.InfoFeed.AddEntry( message );
	}
}

public sealed partial class DNA : Component
{
	public new int Id { get; private set; }
	private static int _internalId = Game.Random.Int( 0, 500 );

	public string SourceName { get; private set; }
	public Player TargetPlayer { get; set; }
	public TimeUntil TimeUntilDecayed { get; private set; }

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		Id = _internalId++;

		var corpse = Components.Get<Corpse>( FindMode.InSelf );
		if ( corpse is not null )
		{
			var lastAttacker = corpse.Player?.LastAttacker?.Components.Get<Player>();
			var distance = lastAttacker is not null
				? Vector3.DistanceBetween( corpse.Player.WorldPosition, lastAttacker.WorldPosition ).SourceUnitsToMeters()
				: 0f;

			SourceName = $"{corpse.Player?.SteamName}'s corpse";
			TimeUntilDecayed = MathF.Pow( 0.74803f * distance, 2 ) + 100;
		}
		else
		{
			SourceName = TypeLibrary.GetType( GetType() )?.ClassName ?? "unknown";
			TimeUntilDecayed = float.MaxValue;
		}
	}

	public Component GetTarget()
	{
		if ( TargetPlayer is null || !TargetPlayer.IsValid() )
			return null;

		var decoyComponent = TargetPlayer.Components.Get<DecoyComponent>( FindMode.InSelf );
		if ( decoyComponent?.Decoy is not null && decoyComponent.Decoy.IsValid() )
			return decoyComponent.Decoy;

		return TargetPlayer.IsAlive ? (Component)TargetPlayer : TargetPlayer.Corpse;
	}

	[TTTEvent.Round.Start]
	private void OnRolesAssigned()
	{
		_internalId = Game.Random.Int( 0, 500 );
	}
}

