using Sandbox;
using System;

namespace TTT;

[Category( "Equipment" )]
[ClassName( "ttt_equipment_newtonlauncher" )]
[Title( "Newton Launcher" )]
public partial class NewtonLauncher : Weapon
{
	private float _charge;
	public bool IsCharging { get; private set; }

	public override string SlotText => $"{(int)_charge}%";

	private const float ChargePerSecond = 50f;
	private const float MaxCharge = 100f;
	private const float MaxForwardForce = 700;
	private const float MinForwardForce = 300;
	private const float MaxUpwardForce = 350;
	private const float MinUpwardForce = 100;

	private float _forwardForce;
	private float _upwardForce;

	public override void ActiveEnd( Player player, bool dropped )
	{
		base.ActiveEnd( player, dropped );

		_charge = 0;
		IsCharging = false;
	}

	public override void Simulate()
	{
		if ( TimeSincePrimaryAttack < Info.PrimaryRate )
			return;

		if ( Input.Down( InputAction.PrimaryAttack ) )
		{
			_charge = Math.Min( MaxCharge, _charge + ChargePerSecond * Time.Delta );

			if ( IsCharging )
				return;

			BroadcastChargeStart();
			IsCharging = true;
		}
		else if ( Input.Released( InputAction.PrimaryAttack ) )
		{
			BroadcastChargeFinished();
			IsCharging = false;

			TimeSincePrimaryAttack = 0;
			AttackPrimary();
		}
	}

	protected override void AttackPrimary()
	{
		BroadcastShootEffects();
		Sound.Play( Info.FireSound, WorldPosition );

		_forwardForce = (_charge / 100f * MinForwardForce) - MinForwardForce + MaxForwardForce;
		_upwardForce = (_charge / 100f * MinUpwardForce) - MinUpwardForce + MaxUpwardForce;

		ShootBullet( Info.Spread, _forwardForce / 100f, Info.Damage, 3.0f, Info.BulletsPerFire );

		_charge = 0;
	}

	protected override void OnHit( SceneTraceResult trace )
	{
		base.OnHit( trace );

		var hitPlayer = trace.GameObject?.Components.Get<Player>( FindMode.InAncestors );
		if ( hitPlayer is null )
			return;

		var pushVel = trace.Direction * _forwardForce;
		pushVel = pushVel.WithZ( Math.Max( pushVel.z, _upwardForce ) );

		hitPlayer.CharController.Punch( pushVel );
	}

	[Rpc.Broadcast]
	protected void BroadcastChargeStart()
	{
		ViewModelRenderer?.Set( "charge", true );
	}

	[Rpc.Broadcast]
	protected void BroadcastChargeFinished()
	{
		ViewModelRenderer?.Set( "charge_finished", true );
	}
}

