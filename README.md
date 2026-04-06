<h1><img src="ui/traitor-icon.png" alt="TTT logo" height="200"/></h1>

# [s&box TTT](https://cigarlounge.github.io/)

[![License](https://img.shields.io/badge/license-agreement-red)](https://github.com/CigarLounge/sbox-TTT/blob/main/LICENSE.md)
[![Discord](https://img.shields.io/discord/949508550118481970?label=discord)](https://discord.gg/rrsrakF8N3)
![GitHub Repo stars](https://img.shields.io/github/stars/CigarLounge/sbox-TTT?style=social)

TTT is a mafia-esque multiplayer game created using s&box. It's aimed to be the spiritual successor of [TTT](https://ttt.badking.net/) but developed for [s&box](https://sbox.facepunch.com/news). The goal of this project is to replicate the vanilla TTT experience with some quality of life thrown in (and cigars 🚬).

# Discord
Need to chat with the devs? Join our [discord](https://discord.gg/rrsrakF8N3).

## Gameplay

You can checkout devblog update videos on my [YouTube channel](https://www.youtube.com/channel/UCk2IAm1j9o_3GWrqf537gNg). We also have a [website](https://cigarlounge.github.io/) that showcases some gameplay screenshots.

### Server Config

Admin access can now be managed with a local `admins.json` allowlist in the game data folder.
The game also still honors s&box engine admins, but the in-game admin console and admin commands now accept either source.
You can manage the local allowlist with:
`ttt_admin_add <steamid64>`, `ttt_admin_remove <steamid64>`, and `ttt_admin_list`

Body hanging permissions are controlled by the saved server convar `ttt_hang_body_roles`.
Set it to a comma-separated list of role names, type names, or class names such as `traitor,detective`.
Use `all` or `*` to allow every role to hang bodies.

Movement and bunny hopping are controlled by:
`ttt_bhop_enabled`, `ttt_bhop_autojump`, `ttt_bhop_air_acceleration`, `ttt_bhop_air_control`, `ttt_bhop_ground_friction`, and `ttt_bhop_speed_cap_multiplier`.
Set the speed cap multiplier to `0` to fully remove the bhop jump speed cap.

AFK handling is controlled by:
`ttt_afk_timer`, `ttt_afk_auto_kick`, `ttt_afk_fun_death`, and `ttt_afk_kick_delay`.

Karma affects both outgoing damage and movement speed.
The minimum movement speed penalty floor is controlled by `ttt_karma_min_speed_scale`.

Public RDM tribunal voting is controlled by:
`ttt_tribunal_enabled`, `ttt_tribunal_vote_seconds`, `ttt_tribunal_min_votes`, and `ttt_tribunal_required_ratio`.
When enabled, open RDM reports can be voted on by the server through the in-game Global Tribunal page.

Guilty RDM punishments are controlled by:
`ttt_rdm_guilty_punishments`, `ttt_rdm_slay_rounds`, `ttt_rdm_damage_scale`, `ttt_rdm_damage_rounds`, and `ttt_rdm_ban_minutes`.
Supported punishment values are `slay`, `half_damage`, `kick`, and `ban`.

## [Contributing](https://github.com/CigarLounge/sbox-TTT/wiki/Contributing)

## [License](https://github.com/CigarLounge/sbox-TTT/blob/main/LICENSE.md)

## Special Thanks
[Thanks to Bad King Urgrain for creating "Trouble in Terrorist Town"](https://ttt.badking.net/) for [Garry's Mod](https://gmod.facepunch.com/) for which sbox-TTT takes inspiration from.
