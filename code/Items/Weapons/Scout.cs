using Editor;
using Sandbox;

namespace TTT;

[Category( "Weapons" )]
[ClassName( "ttt_weapon_scout" )]
[EditorModel( "models/weapons/w_spr.vmdl" )]
[Title( "Scout" )]
public class Scout : Weapon
{
	public bool IsScoped { get; private set; }

	private float _defaultFOV;
	private UI.Scope _sniperScopePanel;

	public override void ActiveStart( Player player )
	{
		base.ActiveStart( player );

		IsScoped = false;
		_defaultFOV = Game.ActiveScene?.Camera?.FieldOfView ?? 90f;
	}

	public override void Simulate()
	{
		if ( !IsProxy && Input.Pressed( InputAction.SecondaryAttack ) )
			SetScoped( !IsScoped );

		base.Simulate();
	}

	public override void BuildInput()
	{
		base.BuildInput();

		if ( IsScoped )
			Owner.ViewAngles = Angles.Lerp( Owner.ViewAngles, Owner.ViewAngles, 0.2f );
	}

	protected override void CreateHudElements()
	{
		base.CreateHudElements();

		_sniperScopePanel = new UI.Scope() { ScopePath = "/ui/scout-scope.png" };
	}

	protected override void DestroyHudElements()
	{
		base.DestroyHudElements();

		if ( Game.ActiveScene?.Camera is { } cam )
			cam.FieldOfView = _defaultFOV;

		_sniperScopePanel?.Delete( true );
	}

	private void SetScoped( bool isScoped )
	{
		IsScoped = isScoped;

		if ( IsScoped )
			_sniperScopePanel?.Show();
		else
			_sniperScopePanel?.Hide();

		if ( ViewModelRenderer is not null )
			ViewModelRenderer.Enabled = !IsScoped;

		if ( Game.ActiveScene?.Camera is { } cam )
			cam.FieldOfView = isScoped ? 20f : _defaultFOV;
	}
}
