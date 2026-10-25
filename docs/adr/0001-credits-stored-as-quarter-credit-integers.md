# Credits are a value object stored as integer quarter-credits

Credits only ever move in 0.25 steps (durations are 15-minute multiples), and the Ledger needs exact sums and comparisons. EF Core's SQLite provider stores `decimal` as text, which translates poorly to SQL aggregates, so the domain gets a `Credits` value object (non-negative multiples of 0.25) persisted as an integer count of quarter-credits through an EF value converter.

**Amendment (2026-10-25):** durations are 15-minute multiples, but hours × hourly rate can fall between quarters (15 min, 3 children = 0.4375). Storage stays whole quarter-credits; the calculator rounds the time cost to the nearest 0.25, midpoints up (spec §3A), so `FromDecimal` never sees a non-quarter value.
