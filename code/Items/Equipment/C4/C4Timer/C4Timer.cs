using System;
using Sandbox.UI;

namespace TTT.UI;

// WorldPanel migration deferred to UI pass.
// C4Timer is positioned at the "timer" attachment on the C4 model.
public partial class C4Timer : WorldPanel
{
	private readonly C4Entity _c4;

	public C4Timer( C4Entity c4 )
	{
		_c4 = c4;
	}

	public override void Tick() { }

	protected override int BuildHash() => HashCode.Combine( _c4?.IsArmed, _c4?.TimeUntilExplode );
}
