using Sandbox;
using Sandbox.Physics;
using System.Collections.Generic;
using System.Linq;

namespace TTT;

[Title( "Player Corpse" )]
public sealed partial class Corpse : Component, ICarriableHint
{
	[Sync] public bool HasCredits { get; private set; }
	[Sync] public Player Player { get; set; }

	/// <summary>
	/// Whether or not this corpse has been found by a player
	/// or revealed at the end of a round.
	/// </summary>
	public bool IsFound { get; set; }

	/// <summary>
	/// The player who identified this corpse (does not include covert searches).
	/// </summary>
	public Player Finder { get; private set; }

	public TimeUntil TimeUntilDNADecay { get; private set; }
	public string C4Note { get; private set; }
	public string LastWords { get; private set; }
	public PerkInfo[] Perks { get; private set; }
	public Player[] KillList { get; private set; }
	public bool HasCalledDetective { get; set; } = false;
	public List<SceneParticles> Ropes { get; private set; } = new();
	public List<PhysicsJoint> RopeJoints { get; private set; } = new();

	// Track SteamIds to avoid sending kill information multiple times.
	private readonly HashSet<ulong> _playersWithKillInfo = new();

	public static Corpse Create( Player player )
	{
		if ( !Networking.IsHost )
			return null;

		var go = new GameObject( true, $"Corpse ({player.SteamName})" );
		go.Tags.Add( "interactable", "corpse" );
		go.WorldPosition = player.WorldPosition;
		go.WorldRotation = player.WorldRotation;

		// Copy model renderer from player and set up as ragdoll
		var playerRenderer = player.Components.Get<SkinnedModelRenderer>();
		var renderer = go.Components.Create<SkinnedModelRenderer>();
		if ( playerRenderer is not null )
		{
			renderer.Model = playerRenderer.Model;
			renderer.UseAnimGraph = false;
		}

		// Set up ragdoll physics
		var physics = go.Components.Create<ModelPhysics>();
		physics.Model = renderer.Model;
		physics.Renderer = renderer;

		var corpse = go.Components.Create<Corpse>();
		corpse.Player = player;
		corpse.HasCredits = player.Credits > 0;

		// Apply the stored death force to the ragdoll.
		var deathImpulse = BuildDeathImpulse( player );
		ApplyImpulseToCorpse( physics, deathImpulse );

		// Attach DNA if killed by bullet
		if ( player.LastDamage.HasTag( DamageTags.Bullet )
			&& player.LastAttacker?.Components.TryGet<Player>( out var killer ) == true )
		{
			var dna = go.Components.Create<DNA>();
			dna.Init( killer );
			corpse.TimeUntilDNADecay = dna.TimeUntilDecayed;
		}

		var c4Note = player.Components.Get<C4Note>();
		if ( c4Note is not null )
			corpse.C4Note = c4Note.SafeWireNumber.ToString();

		corpse.Perks = new PerkInfo[player.Perks.Count];
		for ( var i = 0; i < player.Perks.Count; i++ )
			corpse.Perks[i] = player.Perks[i].Info;

		corpse.LastWords = player.LastWords;
		corpse.KillList = player.PlayersKilled.ToArray();
		corpse._playersWithKillInfo.Add( player.SteamId );

		go.NetworkSpawn();

		return corpse;
	}

	/// <summary>
	/// Search this corpse.
	/// </summary>
	/// <param name="searcher">The player searching.</param>
	/// <param name="covert">Whether or not this is a covert search.</param>
	/// <param name="retrieveCredits">Should the searcher retrieve credits.</param>
	public void Search( Player searcher, bool covert = false, bool retrieveCredits = true )
	{
		if ( !Networking.IsHost )
			return;

		var creditsRetrieved = 0;
		retrieveCredits &= searcher.Role.CanUseShop & searcher.IsAlive;

		if ( retrieveCredits && HasCredits )
		{
			searcher.Credits += Player.Credits;
			creditsRetrieved = Player.Credits;
			Player.Credits = 0;
			HasCredits = false;
		}

		BroadcastSendPlayer( searcher.Network.Owner, Player );
		Player.SendRole( searcher.Network.Owner );
		SendKillInfo( searcher );

		// Dead players always covert search.
		covert |= !searcher.IsAlive;

		if ( !covert )
		{
			if ( !IsFound )
			{
				BroadcastSendPlayerToAll( Player );
				Player.IsRoleKnown = true;
			}

			if ( searcher.Role is Detective )
				SendKillInfoToAll();

			if ( !Player.IsConfirmedDead )
				Player.ConfirmDeath( searcher );

			foreach ( var deadPlayer in Player.PlayersKilled )
			{
				if ( deadPlayer.IsConfirmedDead )
					continue;

				deadPlayer.ConfirmDeath( searcher );

				UI.InfoFeed.AddPlayerToPlayerEntry
				(
					searcher,
					deadPlayer,
					"confirmed the death of"
				);
			}

			if ( !IsFound )
			{
				IsFound = true;
				Finder = searcher;
				Event.Run( TTTEvent.Player.CorpseFound, Player );
				BroadcastCorpseFound( searcher );
			}
		}

		BroadcastSearch( searcher.Network.Owner, creditsRetrieved );
	}

	public void SendKillInfo( Player searcher )
	{
		if ( !Networking.IsHost )
			return;

		var connection = searcher.Network.Owner;
		if ( _playersWithKillInfo.Contains( connection.SteamId ) )
			return;

		_playersWithKillInfo.Add( connection.SteamId );

		Player.SendDamageInfo( connection );
		BroadcastSendMiscInfo( connection, KillList, Perks, C4Note, LastWords, TimeUntilDNADecay );

		if ( searcher.Role is Detective )
			BroadcastSendDetectiveInfo( connection, Player.LastSeenPlayer );
	}

	private static Vector3 BuildDeathImpulse( Player player )
	{
		var impulse = player.LastDamage.Force;

		if ( impulse.LengthSquared <= 0.001f )
			return Vector3.Zero;

		var maxImpulse = 3000f;
		if ( impulse.Length > maxImpulse )
			impulse = impulse.Normal * maxImpulse;

		return impulse;
	}

	private static void ApplyImpulseToCorpse( ModelPhysics physics, Vector3 impulse )
	{
		if ( physics?.PhysicsGroup is null || impulse.LengthSquared <= 0.001f )
			return;

		physics.PhysicsGroup.Sleeping = false;
		physics.PhysicsGroup.ApplyImpulse( impulse );
	}

	public void SendKillInfoToAll()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var player in Utils.GetPlayersWhere( _ => true ) )
			SendKillInfo( player );
	}

	[Rpc.Broadcast]
	public void BroadcastCorpseFound( Player finder, bool wasPreviouslyFound = false )
	{
		IsFound = true;
		Finder = finder;

		if ( Finder.IsValid() && !wasPreviouslyFound )
			Event.Run( TTTEvent.Player.CorpseFound, Player );
	}

	[Rpc.Broadcast]
	private void BroadcastSearch( Connection to, int creditsRetrieved = 0 )
	{
		if ( Connection.Local != to )
			return;

		Player.Status = Player.IsConfirmedDead ? PlayerStatus.ConfirmedDead : PlayerStatus.MissingInAction;

		foreach ( var deadPlayer in Player.PlayersKilled )
			deadPlayer.Status = deadPlayer.IsConfirmedDead ? PlayerStatus.ConfirmedDead : PlayerStatus.MissingInAction;

		if ( creditsRetrieved <= 0 )
			return;

		UI.InfoFeed.AddEntry( Player.Local, $"found {creditsRetrieved} credits!" );
	}

	[Rpc.Broadcast]
	private void BroadcastSendMiscInfo( Connection to, Player[] killList, PerkInfo[] perks, string c4Note, string lastWords, TimeUntil dnaDecay )
	{
		if ( Connection.Local != to )
			return;

		if ( killList is not null )
			Player.PlayersKilled = killList.ToList();

		Perks = perks;
		C4Note = c4Note;
		LastWords = lastWords;
		TimeUntilDNADecay = dnaDecay;
	}

	[Rpc.Broadcast]
	private void BroadcastSendPlayer( Connection to, Player player )
	{
		if ( Connection.Local != to )
			return;

		Player = player;
		Player.Corpse = this;
	}

	[Rpc.Broadcast]
	private void BroadcastSendPlayerToAll( Player player )
	{
		Player = player;
		Player.Corpse = this;
	}

	[Rpc.Broadcast]
	private void BroadcastSendDetectiveInfo( Connection to, Player player )
	{
		if ( Connection.Local != to )
			return;

		Player.LastSeenPlayer = player;
	}

	public void RemoveRopeAttachments()
	{
		foreach ( var rope in Ropes )
			rope.Delete();

		foreach ( var joint in RopeJoints )
			joint.Remove();

		Ropes.Clear();
		RopeJoints.Clear();
	}

	protected override void OnDestroy()
	{
		RemoveRopeAttachments();
	}

	float ICarriableHint.HintDistance => Player.MaxHintDistance;

	bool ICarriableHint.CanHint( Player player ) => GameManager.Instance?.State is InProgress or PostRound;

	Sandbox.UI.Panel ICarriableHint.DisplayHint( Player player ) => new UI.CorpseHint( this );

	void ICarriableHint.Tick( Player player )
	{
		var searchButton = GetSearchButton();

		if ( Input.Down( searchButton ) && CanSearch() )
		{
			var hasCorpseInfo = Player.IsValid() && !Player.LastDamage.Equals( default( DamageInfo ) );

			// Dead player wants to view the body — request a covert search from the server.
			if ( !player.IsAlive && !hasCorpseInfo )
				ConvertSearchCmd( GameObject.Id.GetHashCode() );

			if ( Player.IsValid() && hasCorpseInfo )
				UI.FullScreenHintMenu.Instance?.Open( new UI.InspectMenu( this ) );

			return;
		}

		UI.FullScreenHintMenu.Instance?.Close();
	}

	public bool CanSearch()
	{
		if ( Player.Local is not Player player )
			return false;

		if ( GetSearchButton() == InputAction.PrimaryAttack )
			return true;

		return true;
	}

	public static string GetSearchButton()
	{
		var player = Player.Local;

		if ( player?.ActiveCarriable is not Binoculars binoculars )
			return InputAction.Use;

		if ( !binoculars.IsZoomed )
			return InputAction.Use;

		return InputAction.PrimaryAttack;
	}

	[ConCmd( "ttt_convert_search" )]
	private static void ConvertSearchCmd( int goId )
	{
		if ( !Networking.IsHost )
			return;

		if ( Utils.GetPlayersWhere( p => p.Network.Owner == Rpc.Caller ).FirstOrDefault() is not Player player )
			return;

		var corpse = Game.ActiveScene?.GetAllComponents<Corpse>()
			.FirstOrDefault( c => c.GameObject.Id.GetHashCode() == goId );

		if ( corpse is null )
			return;

		corpse.Search( player, true, false );
	}
}
