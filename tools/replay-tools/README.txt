Python reader for BossModReborn binary replays (BLCB/BLOG), ported from RawReplayParserLog.cs.
Needs only Python 3 + libbrotlidec (Linux) - no .NET. Written 2026-11-12 for the First Master Borgny review.

  python3 economy.py <replay.log>        per-fight damage dealt (player/pets), damage taken, pet HP in/out
  python3 borgny.py <replay.log> [-t]    Borgny damage-taken breakdown (+ full timeline with -t)
  python3 pets.py <replay.log> "YYYY-MM-DD HH:MM:SS" "YYYY-MM-DD HH:MM:SS"   boss targets, pet summons, Snarl
  python3 bmrreplay.py <replay.log>      op counts (sanity check)

analyze.py: PLAYER_NAME must match the character. Effect types 3/5/6 are counted as damage, 4 as heal.
On Windows: `pip install brotli` (used automatically when present); otherwise libbrotlidec.so.1 via ctypes on Linux.
