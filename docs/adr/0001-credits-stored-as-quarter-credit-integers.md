# Credits are a value object stored as integer quarter-credits

Credits only ever move in 0.25 steps (durations are 15-minute multiples), and the Ledger needs exact sums and comparisons. EF Core's SQLite provider stores `decimal` as text, which translates poorly to SQL aggregates, so the domain gets a `Credits` value object (non-negative multiples of 0.25) persisted as an integer count of quarter-credits through an EF value converter.
