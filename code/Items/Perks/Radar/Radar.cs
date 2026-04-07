using Sandbox;
using System.Collections.Generic;

namespace TTT;

[Category( "Perks" )]
[ClassName( "ttt_perk_radar" )]
[Title( "Radar" )]
public partial class Radar : Perk
{
	public override string SlotText => ((int)_timeUntilExecution).ToString();

	private readonly float _timeToExecute = 20f;
	private RadarPointData[] _lastPositions;
	private readonly List<UI.RadarPoint> _cachedPoints = new();
	private readonly Color _defaultRadarColor = TeamExtensions.GetColor( Team.Innocents );
	private readonly Vector3 _radarPointOffset = Vector3.Up * 45;
	private RealTimeUntil _timeUntilExecution = 0f;

	protected override void OnDestroy()
	{
		if ( !IsProxy )
			UI.WorldPoints.Instance?.DeletePoints<UI.RadarPoint>();
	}

	[Event.Tick]
	private void OnTick()
	{
		if ( !Networking.IsHost )
			return;

		var owner = Components.Get<Player>( FindMode.InSelf );
		if ( owner is null )
			return;

		if ( !_timeUntilExecution )
			return;

		ScanAndSend( owner );
		_timeUntilExecution = _timeToExecute;
	}

	private void ScanAndSend( Player owner )
	{
		var ownerConnection = owner.Network.Owner;
		if ( ownerConnection is null )
			return;

		var pointData = new List<RadarPointData>();

		foreach ( var player in Utils.GetPlayersWhere( p => p.IsAlive && p != owner ) )
		{
			if ( !player.CanHint( owner ) )
				continue;

			pointData.Add( new RadarPointData
			{
				Position = player.WorldPosition + _radarPointOffset,
				Color = player.Role == owner.Role ? owner.Role.Info.Color : _defaultRadarColor
			} );
		}

		if ( owner.Team != Team.Traitors )
		{
			foreach ( var decoy in Scene.GetAllComponents<DecoyEntity>() )
			{
				pointData.Add( new RadarPointData
				{
					Position = decoy.WorldPosition,
					Color = _defaultRadarColor
				} );
			}
		}

		BroadcastRadarPositions( ownerConnection, pointData.ToArray() );
	}

	private void UpdateDisplay()
	{
		ClearRadarPoints();

		if ( _lastPositions.IsNullOrEmpty() )
			return;

		foreach ( var radarData in _lastPositions )
			_cachedPoints.Add( new UI.RadarPoint( radarData ) );
	}

	private void ClearRadarPoints()
	{
		foreach ( var radarPoint in _cachedPoints )
			radarPoint.Delete( true );

		_cachedPoints.Clear();
	}

	[Rpc.Broadcast]
	public static void BroadcastRadarPositions( Connection to, RadarPointData[] points )
	{
		if ( Connection.Local != to )
			return;

		var player = Player.Local;
		if ( player is null )
			return;

		var radar = player.Components.Get<Radar>( FindMode.InSelf );
		if ( radar is null )
			return;

		radar._lastPositions = points;
		radar.UpdateDisplay();
	}
}

public struct RadarPointData
{
	public Color Color;
	public Vector3 Position;
}
