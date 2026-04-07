using Sandbox;
using Sandbox.Diagnostics;
using System.Collections.Generic;

namespace TTT;

public enum PlayerStatus
{
	Alive,
	MissingInAction,
	ConfirmedDead,
	Dead,
	Spectator
}

public partial class Player
{
	[Sync] public Corpse Corpse { get; internal set; }
	public Player Confirmer { get; private set; }
	public bool IsAlive => Status == PlayerStatus.Alive;
	public bool IsMissingInAction => Status == PlayerStatus.MissingInAction;
	public bool IsConfirmedDead => Status == PlayerStatus.ConfirmedDead;
	public Player LastSeenPlayer { get; internal set; }
	public List<Player> PlayersKilled { get; internal set; } = new();

	private string _lastWords;
	private TimeSince _timeSinceLastWords;
	public string LastWords
	{
		get
		{
			if ( _timeSinceLastWords > 3 )
				_lastWords = string.Empty;

			return _lastWords;
		}
		set
		{
			if ( !IsAlive )
				return;

			_timeSinceLastWords = 0;
			_lastWords = value;
		}
	}

	[Sync]
	private PlayerStatus _statusSync { get; set; }

	private PlayerStatus _status;
	public PlayerStatus Status
	{
		get => _status;
		set
		{
			if ( _status == value )
				return;

			var oldStatus = _status;
			_status = value;

			if ( Networking.IsHost )
				_statusSync = value;

			Event.Run( TTTEvent.Player.StatusChanged, this, oldStatus );
		}
	}

	/// <summary>
	/// Sets Status to ConfirmedDead and syncs it to everyone.
	/// </summary>
	public void ConfirmDeath( Player confirmer = null )
	{
		if ( !Networking.IsHost )
			return;

		Confirmer = confirmer;
		Status = PlayerStatus.ConfirmedDead;

		BroadcastConfirmDeath( confirmer );
	}

	/// <summary>
	/// Reveals the player's role. If MIA, confirms death and sends corpse info.
	/// </summary>
	public void Reveal()
	{
		if ( !Networking.IsHost )
			return;

		IsRoleKnown = true;

		if ( IsMissingInAction )
			ConfirmDeath();

		if ( Corpse.IsValid() && !Corpse.IsFound )
		{
			Corpse.IsFound = true;
			Corpse.BroadcastFound( null );
		}
	}

	/// <summary>
	/// If MIA, updates status for this client's owner and all Traitors.
	/// </summary>
	public void UpdateMissingInAction()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var player in Utils.GetPlayersWhere( p => !p.IsAlive || p.Team == Team.Traitors ) )
			UpdateStatus( player.Network.Owner );
	}

	public void UpdateStatus( Connection to = null )
	{
		if ( !Networking.IsHost )
			return;

		if ( to is null )
			BroadcastSetStatus( Status );
		else
			BroadcastSetStatusSingle( to, Status );
	}

	private void CheckLastSeenPlayer()
	{
		if ( HoveredPlayer is Player player && player.CanHint( this ) )
			LastSeenPlayer = player;
	}

	private void ResetConfirmationData()
	{
		Confirmer = null;
		Corpse = null;
		LastSeenPlayer = null;
		PlayersKilled.Clear();
	}

	[Rpc.Broadcast]
	private void BroadcastConfirmDeath( Player confirmer )
	{
		Confirmer = confirmer;
		Status = PlayerStatus.ConfirmedDead;
	}

	[Rpc.Broadcast]
	private void BroadcastSetStatus( PlayerStatus status )
	{
		Status = status;
	}

	[Broadcast( NetPermission.HostOnly )]
	private void BroadcastSetStatusSingle( Connection to, PlayerStatus status )
	{
		if ( Rpc.Caller == to )
			Status = status;
	}
}

