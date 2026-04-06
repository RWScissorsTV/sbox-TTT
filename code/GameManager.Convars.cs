using Sandbox;
using System;
using System.Linq;

namespace TTT;

public partial class GameManager
{
#if DEBUG
	#region Debug
	[ConVar.Server( "ttt_round_debug", Help = "Stop the in progress round from ending.", Saved = true )]
	public static bool PreventWin { get; set; }
	#endregion
#endif

	#region Round
	[ConVar.Server( "ttt_preround_time", Help = "The length of the preround time.", Saved = true )]
	public static int PreRoundTime { get; set; } = 20;

	[ConVar.Server( "ttt_inprogress_time", Help = "The length of the in progress round time.", Saved = true )]
	public static int InProgressTime { get; set; } = 300;

	[ConVar.Server( "ttt_inprogress_secs_per_death", Help = "The number of seconds to add to the in progress round timer when someone dies.", Saved = true )]
	public static int InProgressSecondsPerDeath { get; set; } = 15;

	[ConVar.Server( "ttt_postround_time", Help = "The length of the postround time.", Saved = true )]
	public static int PostRoundTime { get; set; } = 15;

	[ConVar.Server( "ttt_mapselection_time", Help = "The length of the map selection period.", Saved = true )]
	public static int MapSelectionTime { get; set; } = 15;
	#endregion

	#region Map
	[ConVar.Server( "ttt_default_map", Help = "The default map to swap to if no maps are found.", Saved = true )]
	public static string DefaultMap { get; set; } = "facepunch.flatgrass";

	[ConVar.Server( "ttt_rtv_threshold", Help = "The percentage of players needed to RTV.", Saved = true )]
	public static float RTVThreshold { get; set; } = 0.66f;

	[ConVar.Replicated( "ttt_round_limit", Help = "The maximum amount of rounds that can be played before a map vote is forced.", Saved = true )]
	public static int RoundLimit { get; set; } = 6;

	[ConVar.Replicated( "ttt_time_limit", Saved = true, Help = "The number of seconds before a map vote is forced." ), Change( nameof( UpdateTimeLimit ) )]
	private static int TimeLimit { get; set; } = 4500;

	public static void UpdateTimeLimit( int _, int newValue )
	{
		Current.TimeUntilMapSwitch = newValue;
	}
	#endregion

	#region Minimum Players
	[ConVar.Replicated( "ttt_min_players", Help = "The minimum players to start the game.", Saved = true )]
	public static int MinPlayers { get; set; } = 2;
	#endregion

	#region Movement
	[ConVar.Replicated( "ttt_bhop_enabled", Help = "Enables bunny hopping friendly movement tuning.", Saved = true )]
	public static bool BhopEnabled { get; set; } = true;

	[ConVar.Replicated( "ttt_bhop_autojump", Help = "Lets players hold jump to continuously bunny hop.", Saved = true )]
	public static bool BhopAutoJump { get; set; } = true;

	[ConVar.Replicated( "ttt_bhop_air_acceleration", Help = "Air acceleration used while bhop movement is enabled.", Saved = true )]
	public static float BhopAirAcceleration { get; set; } = 85f;

	[ConVar.Replicated( "ttt_bhop_air_control", Help = "Air control used while bhop movement is enabled.", Saved = true )]
	public static float BhopAirControl { get; set; } = 30f;

	[ConVar.Replicated( "ttt_bhop_ground_friction", Help = "Ground friction used while bhop movement is enabled.", Saved = true )]
	public static float BhopGroundFriction { get; set; } = 4.5f;

	[ConVar.Replicated( "ttt_bhop_speed_cap_multiplier", Help = "Caps hop speed to DefaultSpeed * value. Set to 0 to disable the cap.", Saved = true )]
	public static float BhopSpeedCapMultiplier { get; set; } = 0f;
	#endregion

	#region AFK Timers
	[ConVar.Replicated( "ttt_afk_timer", Help = "The amount of time before a player is forced to being a spectator.", Saved = true )]
	public static int AFKTimer { get; set; } = 180;

	[ConVar.Server( "ttt_afk_auto_kick", Help = "When enabled, AFK players are kicked after the AFK death sequence instead of only becoming forced spectators.", Saved = true )]
	public static bool AfkAutoKick { get; set; } = false;

	[ConVar.Server( "ttt_afk_fun_death", Help = "When enabled, AFK players trigger a theatrical self-destruct before being removed from the round.", Saved = true )]
	public static bool AfkFunDeath { get; set; } = true;

	[ConVar.Server( "ttt_afk_kick_delay", Help = "How long to wait after the AFK death sequence before kicking the player.", Saved = true )]
	public static float AfkKickDelay { get; set; } = 1.5f;
	#endregion

	#region Credits
	[ConVar.Server( "ttt_credits_award_pct", Help = "When this percentage of Innocents are dead, Traitors are given credits.", Saved = true )]
	public static float CreditsAwardPercentage { get; set; } = 0.35f;

	[ConVar.Server( "ttt_credits_award_size", Help = "The number of credits awarded when the percentage is reached.", Saved = true )]
	public static int CreditsAwarded { get; set; } = 100;

	[ConVar.Server( "ttt_credits_traitordeath", Help = "The number of credits Detectives receive when a Traitor dies.", Saved = true )]
	public static int DetectiveTraitorDeathReward { get; set; } = 100;

	[ConVar.Server( "ttt_credits_detectivekill", Help = "The number of credits a Traitor receives when they kill a Detective.", Saved = true )]
	public static int TraitorDetectiveKillReward { get; set; } = 100;
	#endregion

	#region Voice Chat
	[ConVar.Replicated( "ttt_proximity_chat", Saved = true ), Change( nameof( UpdateVoiceChat ) )]
	public static bool ProximityChat { get; set; }

	public static void UpdateVoiceChat( bool _, bool newValue )
	{
		foreach ( var client in Game.Clients )
		{
			if ( client.Pawn is not Player player || !player.IsAlive )
				continue;

			client.Voice.WantsStereo = newValue;
		}
	}
	#endregion

	#region Corpse Interaction
	[ConVar.Server( "ttt_hang_body_roles", Help = "Comma-separated list of roles allowed to hang bodies. Accepts role titles, type names, or class names. Use '*' or 'all' to allow everyone.", Saved = true )]
	public static string HangBodyRoles { get; set; } = "traitor,detective,innocent";

	public static bool CanHangBodies( Role role )
	{
		if ( role is null )
			return false;

		var configuredRoles = HangBodyRoles?
			.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
			.Select( value => value.ToLowerInvariant() )
			.ToHashSet();

		if ( configuredRoles is null || configuredRoles.Count == 0 )
			return false;

		if ( configuredRoles.Contains( "*" ) || configuredRoles.Contains( "all" ) )
			return true;

		return configuredRoles.Contains( role.Title.ToLowerInvariant() )
			|| configuredRoles.Contains( role.Info.ClassName.ToLowerInvariant() )
			|| configuredRoles.Contains( role.GetType().Name.ToLowerInvariant() );
	}
	#endregion

	#region Tribunal
	[ConVar.Replicated( "ttt_tribunal_enabled", Help = "Whether public tribunal voting is enabled for RDM reports.", Saved = true )]
	public static bool TribunalEnabled { get; set; } = true;

	[ConVar.Server( "ttt_tribunal_vote_seconds", Help = "How long tribunal voting stays open for a report.", Saved = true )]
	public static int TribunalVoteSeconds { get; set; } = 90;

	[ConVar.Server( "ttt_tribunal_min_votes", Help = "Minimum votes needed before a tribunal outcome can be applied.", Saved = true )]
	public static int TribunalMinVotes { get; set; } = 3;

	[ConVar.Server( "ttt_tribunal_required_ratio", Help = "Vote ratio required for an early guilty or not guilty tribunal verdict.", Saved = true )]
	public static float TribunalRequiredRatio { get; set; } = 0.6f;
	#endregion

	#region RDM Punishments
	[ConVar.Server( "ttt_rdm_guilty_punishments", Help = "Comma-separated punishments for guilty RDM rulings. Supports slay, half_damage, kick, ban.", Saved = true )]
	public static string RdmGuiltyPunishments { get; set; } = "slay,half_damage";

	[ConVar.Server( "ttt_rdm_slay_rounds", Help = "How many upcoming rounds a guilty player should be slain for.", Saved = true )]
	public static int RdmSlayRounds { get; set; } = 1;

	[ConVar.Server( "ttt_rdm_damage_scale", Help = "Damage multiplier applied during reduced-damage punishment rounds.", Saved = true )]
	public static float RdmDamageScale { get; set; } = 0.5f;

	[ConVar.Server( "ttt_rdm_damage_rounds", Help = "How many upcoming rounds reduced-damage punishment should last.", Saved = true )]
	public static int RdmDamageRounds { get; set; } = 1;

	[ConVar.Server( "ttt_rdm_ban_minutes", Help = "Ban duration used when ban is included in guilty RDM punishments.", Saved = true )]
	public static int RdmBanMinutes { get; set; } = 60;
	#endregion
}
