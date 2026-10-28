# Network Collapse — art & audio sourcing

All third-party media is opt-in. No copyrighted Uplink artwork or audio is included.

## Recommended free assets (checked 2026-10-28)

| Purpose | Source | Terms |
| --- | --- | --- |
| Terminal panels, sci-fi UI | https://kenney.nl/assets/ui-pack-sci-fi | CC0; 130 assets |
| Standard UI components | https://kenney.nl/assets/ui-pack | CC0; 430 assets |
| Keyboard/mouse hints | https://kenney.nl/assets/input-prompts-pixel-1-bit | CC0; 800 glyphs |
| Terminal clicks and switches | https://kenney.nl/assets/interface-sounds | CC0; 100 OGG sounds |
| Simple click/confirm sounds | https://kenney.nl/assets/ui-audio | CC0; 50 sounds |
| Alarm / sci-fi effects | https://kenney.nl/assets/sci-fi-sounds | CC0; 70 sounds |
| Cyberpunk terminal UI audio | https://willosuna.itch.io/neon-terminal-ui-sfx-lite | Free for commercial use under creator's custom license; don't redistribute as standalone pack |
| Alternative sci-fi UI effects | https://heyheythere.itch.io/scifi-ui-sfx-free | CC BY 4.0, attribution REQUIRED |

Prefer **Kenney CC0** packs for simplicity and easy commercial distribution. Download and inspect the license text that comes with each pack before shipping.

## Working audio system

The game already has original synthesized fallbacks in `NetworkAudio.cs`; sounds work without importing third-party packs. Audio triggers: device selection, building a link, upgrading, repair, incidents, campaign mission rewards and achievements.

To override a cue, place a WAV or OGG file at:
`Assets/Resources/Audio/<cue>.wav` (or `.ogg`).
The following lowercase stems are supported:
`click`, `select`, `confirm`, `error`, `alert`, `linkbuilt`, `repair`, `mission`, `achievement`.

For narrated dialogue, place your *own recordings or properly licensed voice clips* at:
`Assets/Resources/Voice/mission_1.wav` through `mission_6.wav` (OGG also supported).
The game calls these clips on starting a campaign and on reaching the next mission. If a voice clip is unavailable, only the synthesized notification cue is used. No recorded actor voice or speech synthesis model is currently bundled.

## Narration script (original, Russian)

1. `mission_1`: «Диспетчерская, приём. Несколько районов теряют связь. Восстановите устойчивость сети и удержите доступность.»
2. `mission_2`: «Мы обнаружили единую точку отказа. Постройте резервный канал, пока магистраль ещё работает.»
3. `mission_3`: «На узле потеряно питание. Отправьте бригаду и запустите восстановление оборудования.»
4. `mission_4`: «Трафик растёт быстрее расчётов. Увеличьте пропускную способность одного из каналов.»
5. `mission_5`: «Внимание: зафиксирована авария. Ваша задача — удержать сеть в рабочем состоянии.»
6. `mission_6`: «Теперь нужна точная настройка. Измените параметры оборудования и подтвердите конфигурацию.»

Production note: normalize narration louder than ambient and lower than critical alarms; add independent Voice/SFX sliders and subtitles before release.
