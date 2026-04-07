using System;
using Sandbox.UI;

namespace TTT.UI;

public partial class RadarPoint : Panel
{
	private readonly RadarPointData _radarData;
	private string _distance;
	private Vector3 _screenPos;

	public RadarPoint( RadarPointData data )
	{
		if ( WorldPoints.Instance is null )
			return;

		_radarData = data;
		WorldPoints.Instance.AddChild( this );
	}

	protected override void OnUpdate()
	{
		_distance = $"{Player.Local?.WorldPosition.Distance( _radarData.Position ).SourceUnitsToMeters():n0}m";
		_screenPos = _radarData.Position.ToScreen();
	}

	protected override int BuildHash() => HashCode.Combine( _distance, _screenPos );
}
