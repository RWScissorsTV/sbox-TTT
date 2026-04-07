using Sandbox;
using Sandbox.Physics;

namespace TTT;

public class GrabbableCorpse : IGrabbable
{
	public string PrimaryAttackHint => !IsHolding ? "Pickup" : AttachmentHint;
	private string AttachmentHint => !_corpse.Ropes.IsNullOrEmpty() ? "Detach" : _owner.Role.CanAttachCorpses ? "Hang" : string.Empty;
	public string SecondaryAttackHint => IsHolding ? "Drop" : string.Empty;
	public bool IsHolding => _joint.IsValid();

	private readonly Player _owner;
	private readonly Corpse _corpse;
	private PhysicsBody _handPhysicsBody;
	private Sandbox.Physics.FixedJoint _joint;

	public GrabbableCorpse( Player player, Corpse corpse )
	{
		_owner = player;
		_corpse = corpse;

		_handPhysicsBody = new PhysicsBody( Game.PhysicsWorld )
		{
			BodyType = PhysicsBodyType.Keyframed
		};

		var renderer = player.Components.Get<SkinnedModelRenderer>();
		var attachment = renderer?.GetAttachment( Hands.MiddleHandsAttachment );
		if ( attachment.HasValue )
		{
			_handPhysicsBody.Position = attachment.Value.Position;
			_handPhysicsBody.Rotation = attachment.Value.Rotation;
		}

		var modelPhysics = _corpse.Components.Get<ModelPhysics>( FindMode.InSelf );
		var corpseBody = modelPhysics?.PhysicsGroup?.GetBody( 0 );
		if ( corpseBody is not null )
			_joint = PhysicsJoint.CreateFixed( _handPhysicsBody, corpseBody );
	}

	public GameObject Drop()
	{
		if ( _joint.IsValid() )
			_joint.Remove();

		_handPhysicsBody = null;
		return _corpse.GameObject;
	}

	public void Update( Player player )
	{
		if ( _handPhysicsBody is null )
			return;

		foreach ( var spring in _corpse?.RopeJoints )
		{
			if ( Vector3.DistanceBetween( spring.Body1.Position, spring.Point2.LocalPosition ) > Player.UseDistance * 1.5f )
			{
				Drop();
				return;
			}
		}

		var renderer = player.Components.Get<SkinnedModelRenderer>();
		var attachment = renderer?.GetAttachment( Hands.MiddleHandsAttachment );
		if ( attachment.HasValue )
		{
			_handPhysicsBody.Position = attachment.Value.Position;
			_handPhysicsBody.Rotation = attachment.Value.Rotation;
		}
	}

	public void SecondaryAction()
	{
		_corpse.RemoveRopeAttachments();

		if ( !_owner.Role.CanAttachCorpses )
			return;

		var trace = Scene.Trace.Ray( _owner.EyePosition, _owner.EyePosition + _owner.EyeRotation.Forward * Player.UseDistance )
			.IgnoreGameObject( _owner.GameObject )
			.Run();

		if ( !trace.Hit || !trace.Body.IsStatic )
			return;

		var worldLocalPos = trace.Body.Transform.PointToLocal( trace.EndPosition );
		var modelPhysics = _corpse.Components.Get<ModelPhysics>( FindMode.InSelf );
		var corpseBody = modelPhysics?.PhysicsGroup?.GetBody( 0 );
		if ( corpseBody is null )
			return;

		var spring = PhysicsJoint.CreateLength( corpseBody, trace.Body.LocalPoint( worldLocalPos ), 10 );
		spring.SpringLinear = new( 5, 0.3f );
		spring.Collisions = true;
		spring.EnableAngularConstraint = false;
		_corpse.RopeJoints.Add( spring );

		var rope = SceneParticles.Play( _owner.Scene, "particles/rope/rope.vpcf" );
		if ( rope is not null )
		{
			rope.SetPosition( 0, corpseBody.Position );
			rope.SetPosition( 1, trace.EndPosition );
			_corpse.Ropes.Add( rope );
		}

		Drop();
	}
}
