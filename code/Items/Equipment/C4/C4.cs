namespace TTT;

[Category( "Equipment" )]
[ClassName( "ttt_equipment_c4" )]
[HideInEditor]
[Title( "C4" )]
public class C4 : Deployable
{
	protected override bool CanDrop => false;

	protected override void OnDeploy()
	{
		var c4Entity = Components.GetOrCreate<C4Entity>();
		c4Entity.Initialize( PreviousOwner );
	}
}
