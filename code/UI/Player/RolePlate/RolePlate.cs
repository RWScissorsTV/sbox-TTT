using Sandbox;

namespace TTT.UI;

public partial class RolePlate : Component
{
	private RolePlateWorldPanel _roleWorldPanel;

	protected override void OnStart()
	{
		var player = Components.Get<Player>( FindMode.InSelf );
		if ( player is null )
			return;

		_roleWorldPanel = new RolePlateWorldPanel() { Icon = player.Role.Info.IconPath };
	}

	protected override void OnDestroy()
	{
		_roleWorldPanel?.Delete();
		_roleWorldPanel = null;
	}

	protected override void OnUpdate()
	{
		var player = Components.Get<Player>( FindMode.InSelf );
		if ( player is null || _roleWorldPanel is null )
			return;

		_roleWorldPanel.Enabled( Player.Local != player && player.IsAlive );

		if ( !_roleWorldPanel.IsEnabled() )
			return;

		var renderer = player.Components.Get<SkinnedModelRenderer>();
		var boneTransform = renderer?.GetBoneWorldTransform( "head" ) ?? player.WorldTransform;
		boneTransform.Position += Vector3.Up * 20f;
		boneTransform.Rotation = Game.ActiveScene?.Camera?.WorldRotation.RotateAroundAxis( Vector3.Up, 180f ) ?? Rotation.Identity;

		_roleWorldPanel.Transform = boneTransform;
	}
}
