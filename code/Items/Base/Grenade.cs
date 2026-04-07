using Sandbox;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TTT;

public abstract partial class Grenade : Carriable
{
	private enum ThrowType
	{
		None,
		Overhand,
		Underhand
	}

	private TimeUntil _timeUntilExplode;

	public override List<UI.BindingPrompt> BindingPrompts => new()
	{
		new( InputAction.PrimaryAttack, "Throw" ),
		new( InputAction.SecondaryAttack, "Underhand" ),
	};

	protected virtual float SecondsUntilExplode => 3f;
	private ThrowType _throw = ThrowType.None;
	private bool _isThrown = false;
	private Player _previousOwner;

	public override bool CanCarry( Player carrier )
	{
		return !_isThrown && base.CanCarry( carrier );
	}

	public override void Simulate()
	{
		if ( _throw == ThrowType.None )
		{
			if ( Input.Pressed( InputAction.PrimaryAttack ) )
				_throw = ThrowType.Overhand;
			else if ( Input.Pressed( InputAction.SecondaryAttack ) )
				_throw = ThrowType.Underhand;

			if ( _throw != ThrowType.None )
			{
				ViewModelRenderer?.Set( "fire", true );
				_timeUntilExplode = SecondsUntilExplode;
			}

			return;
		}

		if ( _timeUntilExplode || Input.Released( InputAction.PrimaryAttack ) || Input.Released( InputAction.SecondaryAttack ) )
			Throw();
	}

	protected virtual void OnExplode() { }

	protected void Throw()
	{
		if ( !Networking.IsHost )
			return;

		_previousOwner = Owner;
		Owner.Inventory.DropActive();

		var forwards = _previousOwner.EyeRotation.Forward;
		forwards *= _throw == ThrowType.Overhand ? 800f : 300f;

		var upwards = _previousOwner.EyeRotation.Up * 200f;
		var throwVelocity = _previousOwner.CharController.Velocity + forwards + upwards;

		GameObject.WorldPosition = _previousOwner.EyePosition + _previousOwner.EyeRotation.Forward * 3.0f + Vector3.Down * 10f;

		var rb = Components.Get<Rigidbody>( FindMode.InSelf );
		if ( rb is not null )
			rb.Velocity = throwVelocity;

		_isThrown = true;

		_ = ExplodeIn( _timeUntilExplode );
	}

	private async Task ExplodeIn( float seconds )
	{
		await GameTask.DelaySeconds( seconds );

		OnExplode();
		GameObject.Destroy();
	}
}
