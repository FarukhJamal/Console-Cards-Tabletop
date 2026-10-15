# 28 — Player Profiles and Saving

Version 0.1 · 2026-10-15 · Status: Owner's direction recorded; stage P1 (save system) written, to be checked

## The owner's direction (2026-10-15)

- **The platform is the product.** Players buy the platform. It comes with one game (Trap Floor) and the platform's
  components and tools: dice, tokens, template (blank) cards, game box, base box and so on.
- **Games come as DLC or updates**, each with its own cards, decks, numbers and rules. These are read-only.
- **Players get freedom:** change a game's cards and numbers, add new cards, build decks, write rules and make
  whole games of their own. Their cards always sit on **our** components and use **our** tools.
- **Everything is done in the game**, on proper visual screens, not in the Unity Editor.
- **Nothing is platform based.** Everything a player makes or chooses is saved in their **player profile**.
- **Profiles live next to the game:** `<project>/Profiles/` in Unity, a `Profiles` folder beside the .exe in a
  build. There are several named profiles on one computer, and the player picks one. An online account can come
  later.

## Where things are saved

```
Profiles/
  profiles.json                 the profile used last
  <profile>/                    for example player-1-3fa9c2
    profile.json                name and dates
    hints/seen.json             hints shown or dismissed
    games/<game id>/
      cards.json                your new cards and your changes to the game's cards
      decks.json                your decks
      rules/*.json              your house-rule sets
      uploads/                  your pictures (uploads.json lists them by art key)
      packs/<pack>/             packs you added
      settings.json             the active pack
```

- In Unity the `Profiles` folder sits beside `Assets`, so Unity does not import it. Git ignores it (`.gitignore`).
- If the folder beside the game cannot be written (for example a build installed in a protected folder), the
  game's own data folder is used instead, and a console warning names it.
- The first profile, "Player 1", is made automatically. Pictures, packs, house rules and hints saved before
  profiles existed are copied into it once. The old files are left where they were.

## How saving works (code)

One path for every save, in three layers:

| Layer | Class | Job |
|---|---|---|
| Disk | `Presentation/Saving/SaveFiles` | The only code that reads and writes save files. JSON via JsonUtility. A write goes to a temporary file, then replaces the old file, so a crash never leaves half a file (doc 11 §8). A broken file is skipped with a warning and left as it is. |
| Profiles | `Presentation/Saving/ProfileStore`, `ProfileInfo`, `ProfileIndex` | The Profiles folder: list, find and create profiles, and remember the last one. |
| Active profile | `Presentation/Saving/ProfileService` | The single entry point. It says which profile is in use and where its folders are (`GamesFolder`, `HintsSeenFile`), and raises `Changed` when the player switches profile. |

The existing stores keep their jobs and get their folder from the active profile:

| Store | Saves | Folder from |
|---|---|---|
| `ContentPackStore.ForProfile(profiles)` | pictures, packs, active pack, `cards.json`, `decks.json` | `ProfileService.GamesFolder` |
| `HouseRulesStore` | house-rule sets | the content store's game folder |
| `HintSeenStore.ForProfile(profiles)` | hints seen | `ProfileService.HintsSeenFile` |

Rules for new saving code:

- Never write a file directly; use `SaveFiles`.
- Never pick a folder yourself; ask `ProfileService`.
- Every save file has a `formatVersion`. A file made by a newer version is not used, and it is never overwritten
  without the player saving again.
- Save when the player changes something, never every frame.
- Saving never stops play: a failed save is a warning.

## Stages

| Stage | Scope | Status |
|---|---|---|
| P1 | Save system: profiles next to the game, one save path (`SaveFiles`), the profile service, every existing save (pictures, packs, house rules, hints) moved into the profile, cards and decks files per game | Written, to be checked (not compiled yet) |
| P2 | Profile screen in the game: pick a profile, make a new one, rename; the table's saves switch with it | Next |
| Later | Match saves (doc 11) go in the profile too: `games/<game id>/saves/` | Planned |
