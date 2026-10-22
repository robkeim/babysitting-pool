const QUARTERS_PER_CREDIT = 4

/** Quarter-credits (ADR 0001) shown as credits with two decimals: 41 -> "10.25". */
export function formatCredits(quarterCredits: number): string {
  return (quarterCredits / QUARTERS_PER_CREDIT).toFixed(2)
}

/** Stand-in until the co-op time zone setting exists (spec §2A): instants are shown in UTC. */
export function formatInstant(iso: string): string {
  const formatted = new Intl.DateTimeFormat('en-US', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'UTC',
  }).format(new Date(iso))
  return `${formatted} UTC`
}
