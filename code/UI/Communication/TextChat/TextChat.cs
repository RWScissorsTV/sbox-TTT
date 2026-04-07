using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.UI;

namespace TTT.UI;

public partial class TextChat : Panel
{
	public static TextChat Instance;

	public bool IsOpen
	{
		get => HasClass( "open" );
		set
		{
			SetClass( "open", value );
			if ( value )
			{
				Input.Focus();
				Input.Text = string.Empty;
				Input.Label.SetCaretPosition( 0 );
			}
		}
	}

	private static readonly Color _allChatColor = PlayerStatus.Alive.GetColor();
	private static readonly Color _spectatorChatColor = PlayerStatus.Spectator.GetColor();

	private const int MaxItems = 100;
	private const float MessageLifetime = 10f;

	private Panel Canvas { get; set; }
	private TextEntry Input { get; set; }

	private readonly Queue<TextChatEntry> _entries = new();

	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );

		Canvas.PreferScrollToBottom = true;
		Input.AcceptsFocus = true;
		Input.AllowEmojiReplace = true;
		Input.OnTabPressed += OnTabPressed;

		Instance = this;
	}

	public override void Tick()
	{
		var player = Player.Local;
		if ( player is null )
			return;

		if ( Sandbox.Input.Pressed( InputAction.Chat ) )
			Open();

		if ( !IsOpen )
			return;

		Player.SendAfkHeartbeat();

		switch ( player.CurrentChannel )
		{
			case Channel.All:
				Input.Style.BorderColor = _allChatColor;
				return;
			case Channel.Spectator:
				Input.Style.BorderColor = _spectatorChatColor;
				return;
			case Channel.Team:
				Input.Style.BorderColor = player.Role.Color;
				return;
		}
	}

	private void AddEntry( TextChatEntry entry )
	{
		Canvas.AddChild( entry );
		Canvas.TryScrollToBottom();

		entry.BindClass( "stale", () => entry.Lifetime > MessageLifetime );

		_entries.Enqueue( entry );
		if ( _entries.Count > MaxItems )
			_entries.Dequeue().Delete();
	}

	private void Open()
	{
		AddClass( "open" );
		Input.Focus();
		Canvas.TryScrollToBottom();
	}

	private void Close()
	{
		RemoveClass( "open" );
		Input.Blur();
		Input.Text = string.Empty;
		Input.Label.SetCaretPosition( 0 );
	}

	private void Submit()
	{
		var message = Input.Text.Trim();
		Input.Text = "";

		Close();

		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		if ( message == "!rtv" && Connection.Local.HasRockedTheVote() )
		{
			AddInfoEntry( "You have already rocked the vote!" );
			return;
		}

		SendChat( message );
	}

	[ConCmd( "ttt_say" )]
	public static void SendChat( string message )
	{
		if ( !Networking.IsHost )
			return;

		var player = Utils.GetPlayersWhere( p => p.Network.Owner == Rpc.Caller ).FirstOrDefault();
		if ( player is null )
			return;

		if ( message == "!rtv" )
		{
			GameManager.RockTheVote();
			return;
		}

		if ( !player.IsAlive )
		{
			if ( GameManager.Instance?.State is InProgress )
			{
				foreach ( var conn in Utils.GetPlayersWhere( p => !p.IsAlive ).Select( p => p.Network.Owner ).Where( c => c is not null ) )
					BroadcastChatEntryTo( conn, player.SteamId, player.SteamName, message, Channel.Spectator );
			}
			else
			{
				BroadcastChatEntryAll( player.SteamId, player.SteamName, message, Channel.Spectator );
			}
			return;
		}

		if ( player.CurrentChannel == Channel.All )
		{
			player.LastWords = message;
			BroadcastChatEntryAll( player.SteamId, player.SteamName, message, player.CurrentChannel, player.IsRoleKnown ? player.Role.Info.ResourceId : -1 );
		}
		else if ( player.CurrentChannel == Channel.Team && player.Role.CanTeamChat )
		{
			foreach ( var conn in Utils.GetPlayersWhere( p => p.Team == player.Team ).Select( p => p.Network.Owner ).Where( c => c is not null ) )
				BroadcastChatEntryTo( conn, player.SteamId, player.SteamName, message, player.CurrentChannel, player.Role.Info.ResourceId );
		}
	}

	[Rpc.Broadcast]
	public static void BroadcastChatEntryTo( Connection to, ulong playerId, string playerName, string message, Channel channel, int roleId = -1 )
	{
		if ( Connection.Local != to )
			return;

		AddChatEntryLocal( playerId, playerName, message, channel, roleId );
	}

	[Rpc.Broadcast]
	public static void BroadcastChatEntryAll( ulong playerId, string playerName, string message, Channel channel, int roleId = -1 )
	{
		AddChatEntryLocal( playerId, playerName, message, channel, roleId );
	}

	private static void AddChatEntryLocal( ulong playerId, string playerName, string message, Channel channel, int roleId = -1 )
	{
		switch ( channel )
		{
			case Channel.All:
				Instance?.AddEntry( new TextChatEntry( playerId, playerName, message, ResourceLibrary.Get<RoleInfo>( roleId )?.Color ?? _allChatColor ) );
				return;
			case Channel.Team:
				Instance?.AddEntry( new TextChatEntry( playerId, $"(TEAM) {playerName}", message, ResourceLibrary.Get<RoleInfo>( roleId )?.Color ?? _allChatColor ) );
				return;
			case Channel.Spectator:
				Instance?.AddEntry( new TextChatEntry( playerId, playerName, message, _spectatorChatColor ) );
				return;
		}
	}

	[Rpc.Broadcast]
	public static void BroadcastInfoEntry( string message )
	{
		Instance?.AddEntry( new TextChatEntry( message, Color.FromBytes( 253, 196, 24 ) ) );
	}

	[Rpc.Broadcast]
	public static void BroadcastInfoEntryTo( Connection to, string message )
	{
		if ( Connection.Local != to )
			return;

		Instance?.AddEntry( new TextChatEntry( message, Color.FromBytes( 253, 196, 24 ) ) );
	}

	public static void AddInfoEntry( string message )
	{
		Instance?.AddEntry( new TextChatEntry( message, Color.FromBytes( 253, 196, 24 ) ) );
	}

	private void OnTabPressed()
	{
		var player = Player.Local;
		if ( player is null || !player.IsAlive )
			return;

		if ( player.Role.CanTeamChat )
			player.CurrentChannel = player.CurrentChannel == Channel.All ? Channel.Team : Channel.All;
	}
}

public partial class TextEntry : Sandbox.UI.TextEntry
{
	public event Action OnTabPressed;

	public override void OnButtonTyped( ButtonEvent e )
	{
		if ( e.Button == "tab" )
		{
			OnTabPressed?.Invoke();
			return;
		}

		base.OnButtonTyped( e );
	}
}

