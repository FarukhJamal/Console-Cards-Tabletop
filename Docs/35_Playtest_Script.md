# 35 — Trap Floor Play-test Script (full build, 2026-10-22)

Version 1.0 · 2026-10-22 · For the owner, in the Unity Editor, Play mode. Covers everything built up to and including the
2026-10-22 batch (doc 27 rows 32–44). Each step says what to do, then what you should see. Note anything different and
which step it was.

**Before you start**
1. Let Unity compile. Paste any red Console errors.
2. Run **Console Cards > UI > Create Missing Gameplay UI Prefabs**. This adds the WIN MODE and TURNS rows to New
   table…. Then run **Console Cards > Feedback > Create Missing Persona Prefabs (Final World)**.
3. Table menu > New table…. Under PLAYERS you should see **WIN MODE < Regular >** and, with 3 or more players,
   **TURNS < Clockwise >**.

---

## Part A — Main gameplay (Easy · Regular · 4 players · Clockwise)

**Setup and turn start**
1. New table… → Trap Floor, Easy, 4 players, WIN MODE Regular, TURNS Clockwise → Start.
   → 4 Consoles, a 6×6 floor, two d6 and a d4. A message: "PLAYER 1'S TURN: draw up to 10 Controller Cards and 3
   Abilities to buy, then move or use an Ability."
2. Open the turn panel. → A row **Draw up to 10 (+10)**. Press it. → 10 cards come into the hand, and the row goes.
3. Draw 3 cards from the Ability deck by hand. Buy one by paying from the hand through the Console, or put them back.

**Landing on tiles**
4. Move your pawn onto a face-down tile.
   - **Trap** → popup "TRAP • NOW" or "TRAP • NEXT ROUND", the effect in plain words, and **Apply**. If you hold a
     Reaction, there is also **Use a Reaction…**. Press Apply. → The effect happens: Fall (out for this floor), or
     Spring/Crane (the lit tile, then move there or End Turn moves you), or a note for next round (Blind/Slow/Sticky).
   - **Key** → popup with only **Take Key**. → "… claimed … KEYS 1 / 1".
   - **Ability** → popup with **Take it** and **Reject**. Take it → pay (choose cards, put them in your Console,
     Confirm Payment). → The card stays under your pawn.
   - **Friend** → the same popup; Take it costs nothing.
5. Reject a prize. → "… left …". Next turn it is not offered to you again while you stay on it.
6. Take a prize, End Turn, then move off it on your next turn. → The card goes to your hand and its space is empty.
7. Step on a Trap with a Spring. Apply it, then put your pawn on another tile. → It stays lit, with an amber "Move not
   allowed" warning. End Turn. → The pawn hops to the lit tile.
8. Blind: on your next turn just throw the d4 (no button). → "BLIND DIRECTION: …"; the lit tiles go that way only.
   The Draw row says "Blind: no draw this turn".

**Round end and collapse**
9. After the 4th player's End Turn. → "ROUND 1 ENDED — any player: roll the two floor dice (column and row) to
   collapse a tile."
10. Move a pawn now. → Amber: "The round is over: roll the two floor dice … Press Undo to take that move back."
    Press Undo.
11. Throw both floor dice. → The tile they point at is marked. Popup "THE FLOOR COLLAPSES — TILE (x,y)" with
    **Collapse**, and a turn-panel row "Collapse tile (x,y)".
12. Throw the dice again before pressing. → The marker and popup move to the new tile.
13. Press Collapse. → "Floorfall rolled …; Floor (x,y) collapsed". A pawn standing there falls: red "ELIMINATED".
14. Lift the collapsed tile off and set it aside (or press End Floor Turn). → Round 2 starts with Player 1.
15. Dice beyond the floor (on a smaller floor) or on a hole. → "roll … again", with no popup.

**Exits and the next phase**
16. Take a Key, then land on the Secret Exit with it. → The floor is won. Popup **STARTING NEXT PHASE**, "Floor 1 won.
    Next: Floor 2 of 4, 5×5 …", and **OK**.
17. Press OK. → A fresh 5×5 floor. The win mode and the turn direction stay.
18. On a later floor, put every player still in on the Grand Exit with the Keys. → Floor won → STARTING NEXT PHASE.
19. Land on the Grand Exit before everyone is there. → Amber: "GRAND EXIT — n here … waiting for …. Keys n / n."
20. Knock everyone out on a floor. → "Everyone is out: the floor is lost" and the popup at once.
21. Win or lose the 3×3. → "YOU WIN THE GAME" or "GAME OVER", with Play Again.

**Turn direction**
22. New table… with 4 players and TURNS Anticlockwise. → Player 1 first, then 4, 3, 2.
23. With 2 players the TURNS row is hidden.

---

## Part B — The modes

**Team**
24. New table… → Hard, WIN MODE Team, 3 players.
25. Collect 3 Keys between you. Land one player on the Grand Exit. → "… escaped through the Grand Exit. FLOOR WON." →
    STARTING NEXT PHASE.
26. With fewer Keys. → Amber "GRAND EXIT — locked: the team needs 3 Key(s) …".
27. A player holding a Key lands on the Secret Exit. → The floor is won at once.

**Survival**
28. New table… → Easy, WIN MODE Survival, 3 players. → The status line shows "Easy · Survival game".
29. Before 3 Keys, land on the Grand Exit. → "locked: the team needs 3 Key(s)". (On small floors with fewer Key tiles,
    it needs all the Keys that floor has.)
30. With 3 Keys, land player A on the Grand Exit. → "A ESCAPED! Still to escape: B, C." A's pawn goes beside the
    board, left of the floor, and A gets no more turns.
31. Once the Grand Exit is face up, step on a face-up Trap. → Popup "TRIGGERED AGAIN", with Apply.
32. Step on a face-up Ability or Friend. → "prizes can no longer be taken"; the turn panel has no Take row.
33. B escapes, then C escapes. → FLOOR WON → STARTING NEXT PHASE.
34. Variation: after A escapes, B and C are knocked out. → "Everyone left has escaped or is out: the floor is won."

**Impossible**
35. New table… → Impossible, WIN MODE Survival, 3 players. Before anyone escapes, turns go as on Hard: player, player,
    player, then the round's floor turn.
36. After the first escape. → After every player turn comes a floor turn ("FLOOR TURN — any player: roll …"). Roll,
    Collapse, End Floor Turn. → The next player plays in the same round. After the last player, the round ends.

**Undo**
37. Undo right after: an escape (the pawn comes back and the player is in), a collapse, a Take, and a Key claim. →
    Each goes back one step.

---

## Known limits in this build
- A collapsed tile set aside by End Floor Turn keeps its face (face down if it was never revealed). "Set aside face up"
  is not done yet.
- The next-round effects show as chips after the player's name in the turn message, not as a separate popup.
- Abilities are still drawn 3 from the Ability deck by hand, as asked on 2026-10-21. Only the Controller draw has a button.
- Escaped players are counted as "out" in the turn tracking, so that their turns are skipped. The activity log may
  say "eliminated" for them.
- The win mode and the turn direction are not in saved tables: a saved table loads as Regular, Clockwise. They
  stay for the next floors, Play Again and Reset.
- End Floor Turn waits while the dice point at a tile: press Collapse first, or Undo the roll.
- STARTING NEXT PHASE keeps a "Not yet" button, so the floor can be looked at first. The turn panel then offers
  "Start next phase (5×5)".
