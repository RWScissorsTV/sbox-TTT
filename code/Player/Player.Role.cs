using Sandbox;
using System.Collections.Generic;

namespace TTT;

public partial class Player
{
	private readonly HashSet<ulong> _steamIdsWhoKnowTheRole = new();

	private Role _role;
	public Role Role
	{
		get => _role;
		set
		{
			if ( _role == value )
				return;

			_role?.Deselect( this );
			var oldRole = _role;
			_role = value;

			_isRoleKnown = false;
			_steamIdsWhoKnowTheRole.Clear();

			// Always send the role to this player's client
			if ( Networking.IsHost )
				SendRole( Network.Owner );

			_role.Select( this );

			Event.Run( TTTEvent.Player.RoleChanged, this, oldRole );
		}
	}

	public Team Team => Role.Team;

	private bool _isRoleKnown;
	/// <summary>
	/// Serverside: the role is publicly announced to everyone.<br/>
	/// Clientside: we know this player's actual role.
	/// </summary>
	public bool IsRoleKnown
	{
		get => _isRoleKnown;
		set
		{
			if ( _isRoleKnown == value )
				return;

			if ( Networking.IsHost && value )
				SendRoleToAll();

			_isRoleKnown = value;
		}
	}

	/// <summary>
	/// Sends this player's role to a specific connection.
	/// </summary>
	public void SendRole( Connection to )
	{
		if ( !Networking.IsHost )
			return;

		if ( _steamIdsWhoKnowTheRole.Contains( to.SteamId ) )
			return;

		_steamIdsWhoKnowTheRole.Add( to.SteamId );
		BroadcastSetRole( to, Role.Info );
	}

	/// <summary>
	/// Sends this player's role to all connections.
	/// </summary>
	private void SendRoleToAll()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var connection in Connection.All )
		{
			if ( _steamIdsWhoKnowTheRole.Contains( connection.SteamId ) )
				continue;

			_steamIdsWhoKnowTheRole.Add( connection.SteamId );
		}

		BroadcastSetRoleToAll( Role.Info );
	}

	public void SetRole( RoleInfo roleInfo )
	{
		Role = TypeLibrary.Create<Role>( roleInfo.ClassName );
	}

	[Rpc.Broadcast]
	private void BroadcastSetRole( Connection to, RoleInfo roleInfo )
	{
		if ( Rpc.Caller == to || (IsProxy && !Networking.IsHost) )
		{
			SetRole( roleInfo );
			IsRoleKnown = true;
		}
	}

	[Rpc.Broadcast]
	private void BroadcastSetRoleToAll( RoleInfo roleInfo )
	{
		SetRole( roleInfo );
		IsRoleKnown = true;
	}
}

