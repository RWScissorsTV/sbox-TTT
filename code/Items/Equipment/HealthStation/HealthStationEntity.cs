using Sandbox;
using Sandbox.UI;
using System;

namespace TTT;

public sealed partial class HealthStationEntity : Component, ICarriableHint
{
	[Sync] public float StoredHealth { get; set; } = 200f;

	public const string BeepSound = "health_station-beep";
	private const float HealAmount = 1f;
	private const float TimeUntilNextHeal = 0.2f;
	private const float RechargeAmount = 0.5f;

	public Player Planter { get; private set; }

	private RealTimeSince _timeSinceLastUsage;
	private RealTimeUntil _isHealAvailable;

	public void Initialize( Player planter )
	{
		Planter = planter;
	}

	[Event.Tick]
	private void OnTick()
	{
		if ( !Networking.IsHost || StoredHealth >= 200f )
			return;

		StoredHealth = Math.Min( StoredHealth + RechargeAmount * Time.Delta, 200f );
	}

	private void HealPlayer( Player player )
	{
		if ( StoredHealth <= 0 || !_isHealAvailable )
			return;

		var healthNeeded = Player.MaxHealth - player.Health;
		if ( healthNeeded <= 0 )
			return;

		_timeSinceLastUsage = 0f;
		_isHealAvailable = TimeUntilNextHeal;
		Sound.Play( BeepSound, WorldPosition );

		var healAmount = Math.Min( HealAmount, healthNeeded );
		player.Health += healAmount;
		StoredHealth -= healAmount;
	}

	bool ICarriableHint.CanHint( Player player )
	{
		return StoredHealth > 0 && player.IsAlive && player.Health < Player.MaxHealth;
	}

	Panel ICarriableHint.DisplayHint( Player player ) => new UI.HealthStationHint( this );

	void ICarriableHint.Tick( Player player )
	{
		if ( !Networking.IsHost )
			return;

		if ( !Input.Down( InputAction.Use ) )
			return;

		if ( StoredHealth <= 0 || !player.IsAlive || player.Health >= Player.MaxHealth )
			return;

		HealPlayer( player );
	}
}
