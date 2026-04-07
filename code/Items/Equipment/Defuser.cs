using Sandbox;

namespace TTT;

[Category( "Equipment" )]
[ClassName( "ttt_equipment_defuser" )]
[Title( "Defuser" )]
public class Defuser : Carriable
{
	public override void Simulate()
	{
		if ( !Input.Pressed( InputAction.PrimaryAttack ) )
			return;

		var trace = Scene.Trace.Ray( Owner.EyePosition, Owner.EyePosition + Owner.EyeRotation.Forward * Player.UseDistance )
			.IgnoreGameObject( GameObject )
			.IgnoreGameObject( Owner.GameObject )
			.Run();

		if ( !trace.Hit )
			return;

		var c4 = trace.GameObject?.Components.Get<C4Entity>( FindMode.InSelf );
		if ( c4 is not null && c4.IsArmed )
			c4.Defuse();
	}
}
