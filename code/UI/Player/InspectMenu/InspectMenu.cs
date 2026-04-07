using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TTT.UI;

public partial class InspectMenu : Panel
{
	private Panel IconsContainer { get; set; }
	private readonly Corpse _corpse;
	private InspectEntry _selectedInspectEntry;
	private readonly List<InspectEntry> _inspectionEntries = new();
	private InspectEntry _timeSinceDeath;
	private InspectEntry _dna;

	public InspectMenu( Corpse corpse )
	{
		Assert.NotNull( corpse );
		_corpse = corpse;
	}

	protected override void OnAfterTreeRender( bool firstTime )
	{
		if ( !firstTime )
			return;

		SetupInspectIcons();
	}

	private void SetupInspectIcons()
	{
		var player = _corpse.Player;

		_timeSinceDeath = AddInspectEntry( string.Empty, string.Empty, "/ui/inspectmenu/time.png" );

		var (name, deathImageText, deathActiveText) = GetCauseOfDeathStrings();
		AddInspectEntry( deathImageText, deathActiveText, $"/ui/inspectmenu/{name}.png" );

		var weaponInfo = player.LastAttackerWeaponInfo;
		if ( weaponInfo is not null )
			AddInspectEntry( $"{weaponInfo.Title}", $"It appears a {weaponInfo.Title} was used to kill them.", weaponInfo.IconPath );

		if ( player.LastDamage.IsHeadshot() )
			AddInspectEntry( "Headshot", "The fatal wound was a headshot. No time to scream.", "/ui/inspectmenu/headshot.png" );

		_dna = AddInspectEntry( string.Empty, string.Empty, "/ui/inspectmenu/dna.png" );
		_dna.Enabled( !_corpse.TimeUntilDNADecay );

		if ( player.LastSeenPlayer.IsValid() )
			AddInspectEntry( player.LastSeenPlayer.SteamName,
				$"The last person they saw was {player.LastSeenPlayer.SteamName}... killer or coincidence?",
				"/ui/inspectmenu/lastseen.png" );

		if ( player.PlayersKilled.Count > 0 )
		{
			var activeText = "You found a list of kills that confirms the death(s) of... ";
			for ( var i = 0; i < player.PlayersKilled.Count; ++i )
				activeText += i == player.PlayersKilled.Count - 1 ? $"{player.PlayersKilled[i].SteamName}." : $"{player.PlayersKilled[i].SteamName}, ";
			AddInspectEntry( "Kill List", activeText, "/ui/inspectmenu/killlist.png" );
		}

		if ( !_corpse.C4Note.IsNullOrEmpty() )
			AddInspectEntry( "C4 Defuse Note",
				$"You find a note stating that cutting wire {_corpse.C4Note} will safely disarm the C4.",
				"/ui/inspectmenu/c4note.png" );

		if ( !_corpse.LastWords.IsNullOrEmpty() )
			AddInspectEntry( "Last Words",
				$"Their last words were... \"{_corpse.LastWords}\"",
				"/ui/inspectmenu/lastwords.png" );

		if ( !_corpse.Perks.IsNullOrEmpty() )
		{
			foreach ( var perk in _corpse.Perks )
				AddInspectEntry( perk.Title, $"They were carrying {perk.Title}.", perk.IconPath );
		}

		foreach ( var entry in _inspectionEntries )
		{
			entry.AddEventListener( "onmouseover", () => { _selectedInspectEntry = entry; } );
			entry.AddEventListener( "onmouseout", () => { _selectedInspectEntry = null; } );
		}
	}

	private InspectEntry AddInspectEntry( string iconText, string activeText, string iconPath )
	{
		var entry = new InspectEntry() { Parent = IconsContainer, IconText = iconText, ActiveText = activeText, IconPath = iconPath };
		_inspectionEntries.Add( entry );
		return entry;
	}

	private (string name, string imageText, string activeText) GetCauseOfDeathStrings()
	{
		var causeOfDeath = ("Unknown", "Unknown", "The cause of death is unknown.");
		foreach ( var tag in _corpse.Player.LastDamage.Tags )
		{
			return tag switch
			{
				DamageTags.Bullet => ("Bullet", "Bullet", "This corpse was shot to death."),
				DamageTags.Slash => ("Slash", "Slashed", "This corpse was cut to death."),
				DamageTags.Burn => ("Burn", "Burned", "This corpse has burn marks all over."),
				DamageTags.Vehicle => ("Vehicle", "Vehicle", "This corpse was hit by a vehicle."),
				DamageTags.Fall => ("Fall", "Fell", "This corpse fell from a high height."),
				DamageTags.Explode => ("Explode", "Explosion", "An explosion eviscerated this corpse."),
				DamageTags.Drown => ("Drown", "Drown", "This player drowned to death."),
				_ => ("Unknown", "Unknown", "The cause of death is unknown.")
			};
		}
		return causeOfDeath;
	}

	public override void Tick()
	{
		var timeSinceDeath = _corpse.Player.TimeSinceDeath.Relative.TimerFormat();
		_timeSinceDeath.IconText = $"{timeSinceDeath}";
		_timeSinceDeath.ActiveText = $"They died roughly {timeSinceDeath} ago.";

		_dna.Enabled( !_corpse.TimeUntilDNADecay );
		if ( _dna.IsEnabled() )
		{
			_dna.IconText = $"DNA {_corpse.TimeUntilDNADecay.Relative.TimerFormat()}";
			_dna.ActiveText = $"The DNA sample will decay in {_corpse.TimeUntilDNADecay.Relative.TimerFormat()}.";
		}
	}

	protected override int BuildHash()
	{
		return HashCode.Combine( _corpse.HasCalledDetective, Player.Local?.IsAlive, _selectedInspectEntry?.ActiveText );
	}

	public void CallDetective()
	{
		if ( _corpse.HasCalledDetective )
			return;

		CallDetectivesCmd( _corpse.GameObject.Id.GetHashCode() );
		_corpse.HasCalledDetective = true;
	}

	[ConCmd( "ttt_call_detective" )]
	private static void CallDetectivesCmd( int goId )
	{
		if ( !Networking.IsHost )
			return;

		var corpse = Game.ActiveScene?.GetAllComponents<Corpse>()
			.FirstOrDefault( c => c.GameObject.Id.GetHashCode() == goId );

		if ( corpse is null )
			return;

		var callerName = Rpc.Caller?.Name ?? "Unknown";
		TextChat.BroadcastInfoEntry( $"{callerName} called a Detective to the body of {corpse.Player?.SteamName}." );

		foreach ( var conn in Utils.GetPlayersWhere( p => p.IsAlive && p.Role is Detective )
			.Select( p => p.Network.Owner ).Where( c => c is not null ) )
		{
			BroadcastDetectiveMarker( conn, corpse.WorldPosition );
		}
	}

	[Rpc.Broadcast]
	public static void BroadcastDetectiveMarker( Connection to, Vector3 corpseLocation )
	{
		if ( Connection.Local != to )
			return;

		TimeSince timeSinceCreated = 0;
		WorldPoints.Instance?.AddChild(
			new WorldMarker(
				"/ui/d-call-icon.png",
				() => $"{Player.Local?.WorldPosition.Distance( corpseLocation ).SourceUnitsToMeters():n0}m",
				() => corpseLocation,
				() => timeSinceCreated > 30
			)
		);
	}
}

