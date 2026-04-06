using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Sandbox.UI;

namespace TTT.UI;

public partial class RoleSummary : Panel
{
	public static Panel Instance { get; set; }
	private static readonly JsonSerializerOptions _jsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	private static List<Player> _innocents = new();
	private static List<Player> _detectives = new();
	private static List<Player> _traitors = new();
	private static List<RoundHighlight> _highlights = new();
	private static readonly Dictionary<long, float> _damageDealt = new();
	private static readonly Dictionary<long, float> _burnDamageDealt = new();
	private static readonly Dictionary<long, int> _kills = new();
	private static readonly Dictionary<long, int> _fallDeaths = new();

	public RoleSummary() => Instance = this;

	[TTTEvent.Round.Start]
	private static void OnRoundStart()
	{
		if ( !Game.IsServer )
			return;

		_damageDealt.Clear();
		_burnDamageDealt.Clear();
		_kills.Clear();
		_fallDeaths.Clear();
	}

	[TTTEvent.Player.TookDamage]
	private static void OnPlayerTookDamage( Player victim )
	{
		if ( !Game.IsServer || GameManager.Current.State is not InProgress )
			return;

		if ( victim.LastAttacker is not Player attacker || attacker == victim )
			return;

		_damageDealt[attacker.SteamId] = _damageDealt.GetValueOrDefault( attacker.SteamId ) + victim.LastDamage.Damage;

		if ( victim.LastDamage.HasTag( DamageTags.Burn ) )
			_burnDamageDealt[attacker.SteamId] = _burnDamageDealt.GetValueOrDefault( attacker.SteamId ) + victim.LastDamage.Damage;
	}

	[TTTEvent.Player.Killed]
	private static void OnPlayerKilled( Player victim )
	{
		if ( !Game.IsServer || GameManager.Current.State is not InProgress )
			return;

		if ( victim.LastAttacker is Player attacker && attacker != victim )
			_kills[attacker.SteamId] = _kills.GetValueOrDefault( attacker.SteamId ) + 1;

		if ( victim.LastDamage.HasTag( DamageTags.Fall ) )
			_fallDeaths[victim.SteamId] = _fallDeaths.GetValueOrDefault( victim.SteamId ) + 1;
	}

	[TTTEvent.Round.End]
	private static void OnRoundEnd( Team winningTeam, WinType winType )
	{
		if ( !Game.IsServer )
			return;

		RoleSummary.SendData( JsonSerializer.Serialize( BuildHighlights(), _jsonOptions ) );
	}

	[ClientRpc]
	public static void SendData( string highlightsJson )
	{
		_innocents = Role.GetPlayers<Innocent>().OrderByDescending( p => p.Score ).ToList();
		_detectives = Role.GetPlayers<Detective>().OrderByDescending( p => p.Score ).ToList();
		_traitors = Role.GetPlayers<Traitor>().OrderByDescending( p => p.Score ).ToList();
		_highlights = JsonSerializer.Deserialize<List<RoundHighlight>>( highlightsJson ?? "[]", _jsonOptions ) ?? new();

		Instance?.StateHasChanged();
	}

	private static List<RoundHighlight> BuildHighlights()
	{
		var highlights = new List<RoundHighlight>();

		highlights.Add( BuildTopFloatHighlight( "Most Damage", _damageDealt, "damage dealt", "No meaningful damage dealt." ) );
		highlights.Add( BuildTopIntHighlight( "Most Kills", _kills, "kills", "No kills this round." ) );
		highlights.Add( BuildTopFloatHighlight( "Most Burn Damage", _burnDamageDealt, "burn damage", "Nobody got roasted." ) );
		highlights.Add( BuildFallDeathsHighlight() );

		return highlights;
	}

	private static RoundHighlight BuildTopFloatHighlight( string title, Dictionary<long, float> values, string suffix, string emptyText )
	{
		var topEntry = values
			.Where( entry => entry.Value > 0f )
			.OrderByDescending( entry => entry.Value )
			.FirstOrDefault();

		if ( topEntry.Key == 0 || topEntry.Value <= 0f )
			return new RoundHighlight { Title = title, PrimaryText = "Nobody", SecondaryText = emptyText };

		return new RoundHighlight
		{
			Title = title,
			PrimaryText = GetPlayerName( topEntry.Key ),
			SecondaryText = $"{MathF.Round( topEntry.Value )} {suffix}"
		};
	}

	private static RoundHighlight BuildTopIntHighlight( string title, Dictionary<long, int> values, string suffix, string emptyText )
	{
		var topEntry = values
			.Where( entry => entry.Value > 0 )
			.OrderByDescending( entry => entry.Value )
			.FirstOrDefault();

		if ( topEntry.Key == 0 || topEntry.Value <= 0 )
			return new RoundHighlight { Title = title, PrimaryText = "Nobody", SecondaryText = emptyText };

		return new RoundHighlight
		{
			Title = title,
			PrimaryText = GetPlayerName( topEntry.Key ),
			SecondaryText = $"{topEntry.Value} {suffix}"
		};
	}

	private static RoundHighlight BuildFallDeathsHighlight()
	{
		var fallenPlayers = _fallDeaths
			.Where( entry => entry.Value > 0 )
			.OrderByDescending( entry => entry.Value )
			.Select( entry => entry.Value > 1 ? $"{GetPlayerName( entry.Key )} ({entry.Value})" : GetPlayerName( entry.Key ) )
			.ToList();

		if ( fallenPlayers.Count == 0 )
		{
			return new RoundHighlight
			{
				Title = "Fell To Their Death",
				PrimaryText = "Nobody",
				SecondaryText = "Everyone stuck the landing."
			};
		}

		return new RoundHighlight
		{
			Title = "Fell To Their Death",
			PrimaryText = string.Join( ", ", fallenPlayers ),
			SecondaryText = fallenPlayers.Count == 1 ? "One fatal fall." : $"{fallenPlayers.Count} players took a fatal fall."
		};
	}

	private static string GetPlayerName( long steamId )
	{
		return Game.Clients
			.Select( client => client.Pawn as Player )
			.FirstOrDefault( player => player.IsValid() && player.SteamId == steamId )?.SteamName ?? steamId.ToString();
	}

	internal class RoundHighlight
	{
		public string Title { get; set; }
		public string PrimaryText { get; set; }
		public string SecondaryText { get; set; }
	}
}
