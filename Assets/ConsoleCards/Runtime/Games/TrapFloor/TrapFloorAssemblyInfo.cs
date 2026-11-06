using System.Runtime.CompilerServices;

// AUTH-RULES (2026-11-06): the EditMode tests forge rules states (a Key, an escape, a collapse, another player's
// elimination) exactly as a modified client could, to test the host's guard. The test assembly is Editor-only and is
// never in a player build.
[assembly: InternalsVisibleTo("ConsoleCards.Tests.EditMode")]
