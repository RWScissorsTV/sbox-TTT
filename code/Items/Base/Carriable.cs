using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace TTT;

public enum SlotType
{
	Primary,
	Secondary,
	Melee,
	OffensiveEquipment,
	UtilityEquipment,
	Grenade,
}

[Title( "Carriable" ), Icon( "luggage" )]
public abstract partial class Carriable : Component
{
	public TimeSince TimeSinceDeployed { get; private set; }
	public TimeSince TimeSinceDropped { get; private set; }

	[Sync] public Player Owner { get; set; }

	public Player PreviousOwner { get; private set; }

	[RequireComponent] public ModelRenderer WorldRenderer { get; private set; }
	public SkinnedModelRenderer ViewModelRenderer { get; private set; }
	public ModelRenderer HandsRenderer { get; private set; }

	public CarriableInfo Info { get; private set; }

	/// <summary>
	/// Return the transform we should be spawning particles from.
	/// </summary>
	public virtual Transform EffectTransform
	{
		get
		{
			if ( ViewModelRenderer.IsValid() && Player.Local == Owner )
				return ViewModelRenderer.GetAttachment( "muzzle" ) ?? WorldTransform;

			return WorldRenderer.GetAttachment( "muzzle" ) ?? WorldTransform;
		}
	}

	/// <summary>
	/// The text that will show up in the inventory slot.
	/// </summary>
	public virtual string SlotText => string.Empty;

	/// <summary>
	/// Prompts that appear at the bottom of the user's screen.
	/// </summary>
	public virtual List<UI.BindingPrompt> BindingPrompts => new();

	public bool IsActive => Owner?.ActiveCarriable == this;

	protected override void OnStart()
	{
		Tags.Add( "interactable" );

		var className = TypeLibrary.GetType( GetType() )?.ClassName;
		if ( className.IsNullOrEmpty() )
		{
			Log.Error( this + " doesn't have a class name!" );
			return;
		}

		Info = GameResource.GetInfo<CarriableInfo>( className );
		if ( Info is not null )
			WorldRenderer.Model = Info.WorldModel;
	}

	public virtual void ActiveStart( Player player )
	{
		WorldRenderer.Enabled = true;

		if ( Player.Local == Owner )
		{
			CreateViewModel();
			CreateHudElements();

			ViewModelRenderer?.Set( "deploy", true );
		}

		TimeSinceDeployed = 0;

		if ( !Networking.IsHost )
			return;

		if ( !Components.GetAll<DNA>().Any( dna => dna.TargetPlayer == Owner ) )
			Components.Create<DNA>().TargetPlayer = Owner;
	}

	public virtual void ActiveEnd( Player player, bool dropped )
	{
		if ( !dropped )
			WorldRenderer.Enabled = false;

		if ( !Networking.IsHost )
		{
			DestroyViewModel();
			DestroyHudElements();
		}
	}

	public virtual void Simulate() { }
	public virtual void FrameSimulate() { }
	public virtual void BuildInput() { }

	public virtual bool CanCarry( Player carrier )
	{
		if ( Owner is not null )
			return false;

		if ( carrier == PreviousOwner && TimeSinceDropped < 1f )
			return false;

		return true;
	}

	public virtual void OnCarryStart( Player carrier )
	{
		if ( !Networking.IsHost )
		{
			Info ??= GameResource.GetInfo<CarriableInfo>( TypeLibrary.GetType( GetType() )?.ClassName ?? "" );
			return;
		}

		Owner = carrier;
		GameObject.Parent = carrier.GameObject;
		WorldRenderer.Enabled = false;
	}

	public virtual void OnCarryDrop( Player dropper )
	{
		PreviousOwner = dropper;

		if ( !Networking.IsHost )
			return;

		Owner = null;
		WorldRenderer.Enabled = true;
		GameObject.Parent = null;
		TimeSinceDropped = 0;
		WorldPosition = dropper.WorldPosition;
	}

	public virtual void SimulateAnimator( SkinnedModelRenderer renderer )
	{
		renderer.Set( "holdtype", (int)(Info?.HoldType ?? CitizenAnimationHelper.HoldTypes.None) );
		renderer.Set( "aim_body_weight", 1.0f );
		renderer.Set( "holdtype_handedness", 0 );
	}

	/// <summary>
	/// Create the view model. Override to customize.
	/// </summary>
	public virtual void CreateViewModel()
	{
		if ( Info?.ViewModel is null )
			return;

		var vmGo = new GameObject( true, "ViewModel" );
		ViewModelRenderer = vmGo.Components.Create<SkinnedModelRenderer>();
		ViewModelRenderer.Model = Info.ViewModel;
		// Set first-person rendering layer
		vmGo.Tags.Add( "viewmodel" );

		if ( Info.HandsModel is not null )
		{
			var handsGo = new GameObject( true, "Hands" );
			handsGo.Parent = vmGo;
			HandsRenderer = handsGo.Components.Create<ModelRenderer>();
			HandsRenderer.Model = Info.HandsModel;
			handsGo.Tags.Add( "viewmodel" );
		}
	}

	protected virtual void DestroyViewModel()
	{
		ViewModelRenderer?.GameObject.Destroy();
		ViewModelRenderer = null;
		HandsRenderer?.GameObject.Destroy();
		HandsRenderer = null;
	}

	protected virtual void CreateHudElements() { }
	protected virtual void DestroyHudElements() { }

	protected override void OnDestroy()
	{
		DestroyViewModel();
		DestroyHudElements();
	}

	public bool CanHint( Player player ) => Owner is null;

	public bool OnUse( Player user )
	{
		if ( user is not null )
			user.Inventory.OnUse( this );

		return false;
	}

	public bool IsUsable( Player user ) => Owner is null && user is not null && user.IsAlive;

#if SANDBOX && DEBUG
	private void OnHotload()
	{
		Info = GameResource.GetInfo<CarriableInfo>( TypeLibrary.GetType( GetType() )?.ClassName ?? "" );
	}
#endif
}
