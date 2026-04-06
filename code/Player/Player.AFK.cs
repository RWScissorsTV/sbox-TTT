using Sandbox;
using System;
using System.Collections.Generic;

namespace TTT;

public partial class Player
{
	private TimeSince _timeSinceLastAction = 0f;
	private bool _requestedAfkPunishment;
	private bool _isHandlingAfkPunishment;

	private void CheckAFK()
	{
		if ( Client.IsBot || Spectating.IsForced || _requestedAfkPunishment )
			return;

		if ( !IsAlive )
		{
			_timeSinceLastAction = 0;
			_requestedAfkPunishment = false;
			return;
		}

		var isAnyKeyPressed = false;

		foreach ( var action in InputAction.All )
			isAnyKeyPressed |= Input.Down( action );

		var isMouseMoving = Input.MouseDelta != Vector2.Zero;

		if ( isAnyKeyPressed || isMouseMoving )
		{
			_timeSinceLastAction = 0f;
			_requestedAfkPunishment = false;
			return;
		}

		if ( _timeSinceLastAction > GameManager.AFKTimer )
		{
			_requestedAfkPunishment = true;
			Input.StopProcessing = true;
			TriggerAfkPunishment();
		}
	}

	[ConCmd.Server( Name = "ttt_afk_timeout" )]
	private static void TriggerAfkPunishment()
	{
		var player = ConsoleSystem.Caller?.Pawn as Player;
		if ( !player.IsValid() || player.Client.IsBot || player._isHandlingAfkPunishment )
			return;

		player._isHandlingAfkPunishment = true;
		player.BeginAfkPunishment();
	}

	private async void BeginAfkPunishment()
	{
		Game.AssertServer();

		if ( !IsAlive )
		{
			FinalizeAfkPunishment();
			return;
		}

		if ( !GameManager.AfkAutoKick )
			Client.SetValue( "forced_spectator", true );

		if ( GameManager.AfkFunDeath )
		{
			SetAnimParameter( "b_attack", true );
			Velocity += Vector3.Up * 320f;
			Particles.Create( "particles/discombobulator/explode.vpcf", Position + Vector3.Up * 32f );
			Sound.FromWorld( "discombobulator_explode-1", Position );
			UI.TextChat.AddInfoEntry( To.Everyone, $"{SteamName} was claimed by the idle gods." );

			await GameTask.DelaySeconds( 0.35f );

			if ( IsValid() && IsAlive )
			{
				var tags = new HashSet<string> { DamageTags.Explode, DamageTags.Avoidable };
				var damage = DamageInfo.Generic( float.MaxValue )
					.WithAttacker( this )
					.WithTag( DamageTags.Silent );

				damage.Tags = tags;
				TakeDamage( damage );
			}
		}
		else if ( IsAlive )
		{
			Kill();
		}

		FinalizeAfkPunishment();
	}

	private async void FinalizeAfkPunishment()
	{
		Game.AssertServer();

		if ( GameManager.AfkAutoKick && Client.IsValid() )
		{
			var kickDelay = MathF.Max( GameManager.AfkKickDelay, 0f );
			if ( kickDelay > 0f )
				await GameTask.DelaySeconds( kickDelay );

			if ( Client.IsValid() )
				Client.Kick();
		}

		_requestedAfkPunishment = false;
		_isHandlingAfkPunishment = false;
	}
}
