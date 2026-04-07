using Sandbox;

namespace TTT;

public sealed partial class PoltergeistEntity : Component
{
	[RequireComponent] private ModelRenderer _renderer { get; set; }

	private const int BounceForce = 950;
	private const int MaxBounces = 5;
	private int _bounces = 0;
	private TimeUntil _timeUntilNextBounce = 0f;

	protected override void OnStart()
	{
		_renderer.Model = Model.Load( "models/poltergeist/poltergeist_attachment.vmdl" );
	}

	public void AttachTo( GameObject parent )
	{
		if ( parent is not null )
			GameObject.Parent = parent;
	}

	public override void Tick()
	{
		if ( !Networking.IsHost )
			return;

		if ( _bounces >= MaxBounces )
		{
			GameObject.Destroy();
			return;
		}

		if ( _timeUntilNextBounce )
			Bounce();
	}

	private void Bounce()
	{
		var parentRb = GameObject.Parent?.Components.Get<Rigidbody>();
		if ( parentRb is not null )
		{
			var randDirection = Game.Random.Float( -BounceForce, BounceForce );
			parentRb.PhysicsBody.Velocity = new Vector3( randDirection, randDirection, randDirection );
		}

		_bounces += 1;
		_timeUntilNextBounce = 1.5f;
	}
}
