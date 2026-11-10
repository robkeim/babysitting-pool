#!/usr/bin/env node
// Delivery metrics (docs/metrics.md): reads GitHub through `gh api`, the local gate history and the Claude Code
// transcripts, and prints a Markdown report.
//
//   node scripts/delivery-metrics.mjs [--out docs/metrics.md]
//
// The aggregation functions are pure and exported for tests (scripts/delivery-metrics.test.mjs); only main() does I/O.
import { execFileSync } from 'node:child_process'
import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs'
import { homedir } from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

// USD per million tokens. Update when prices change; the estimate is only as good as this table.
export const PRICES = {
  'claude-fable-5-1': { input: 15, output: 75, cacheRead: 1.5, cacheCreation: 18.75 },
  'claude-opus-5-5': { input: 5, output: 25, cacheRead: 0.5, cacheCreation: 6.25 },
  'claude-sonnet-5-5': { input: 3, output: 15, cacheRead: 0.3, cacheCreation: 3.75 },
  'claude-haiku-5-5': { input: 1, output: 5, cacheRead: 0.1, cacheCreation: 1.25 },
}
export const DEFAULT_PRICE = PRICES['claude-sonnet-5-5']

const HOUR = 3_600_000

/** Percentile by linear interpolation; null for an empty list. */
export function percentile(values, p) {
  if (values.length === 0) return null
  const sorted = [...values].sort((a, b) => a - b)
  const rank = (sorted.length - 1) * p
  const lo = Math.floor(rank)
  const hi = Math.ceil(rank)
  return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo)
}

/** Issue number from `<issue>-<slug>` or `robkeim/<issue>-<slug>`; null when the branch carries none. */
export function branchToTicket(branch) {
  const m = /^(?:[^/]+\/)?(\d+)-/.exec(branch ?? '')
  return m ? Number(m[1]) : null
}

/** Monday (UTC) of the week containing the instant, as YYYY-MM-DD. */
export function weekStart(iso) {
  const d = new Date(iso)
  const monday = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()))
  monday.setUTCDate(monday.getUTCDate() - ((monday.getUTCDay() + 6) % 7))
  return monday.toISOString().slice(0, 10)
}

export function throughputPerWeek(prs) {
  const counts = new Map()
  for (const pr of prs) {
    if (!pr.mergedAt) continue
    const week = weekStart(pr.mergedAt)
    counts.set(week, (counts.get(week) ?? 0) + 1)
  }
  return [...counts].sort(([a], [b]) => a.localeCompare(b)).map(([week, merged]) => ({ week, merged }))
}

/**
 * Cycle times in hours. A PR's issue is the first of its `closingIssues`; PRs without one only
 * contribute to the PR-opened-to-merged series.
 */
export function cycleTimes(prs, issuesByNumber) {
  const prHours = []
  const issueHours = []
  for (const pr of prs) {
    if (!pr.mergedAt) continue
    const merged = Date.parse(pr.mergedAt)
    prHours.push((merged - Date.parse(pr.createdAt)) / HOUR)
    const issue = issuesByNumber.get(pr.closingIssues?.[0])
    if (issue) issueHours.push((merged - Date.parse(issue.createdAt)) / HOUR)
  }
  const summarize = (hours) => ({ n: hours.length, median: percentile(hours, 0.5), p90: percentile(hours, 0.9) })
  return { issueToMerged: summarize(issueHours), prToMerged: summarize(prHours) }
}

/**
 * Rework per ticket: commits pushed after the PR opened, plus follow-up `fix` issues whose body or title
 * mentions the ticket. `commits` carry `committedDate`; the first commit of a PR is never rework.
 */
export function reworkPerTicket(prs, fixIssues) {
  const rows = []
  for (const pr of prs) {
    const ticket = pr.closingIssues?.[0]
    if (!pr.mergedAt || ticket === undefined) continue
    const opened = Date.parse(pr.createdAt)
    const after = (pr.commits ?? []).filter((c) => Date.parse(c.committedDate) > opened).length
    const mention = new RegExp(`#${ticket}(?!\\d)`)
    const followUps = fixIssues.filter((f) => f.number !== ticket && mention.test(`${f.title}\n${f.body ?? ''}`)).length
    rows.push({ ticket, commitsAfterOpen: after, followUpFixes: followUps })
  }
  return rows.sort((a, b) => a.ticket - b.ticket)
}

/** Parses `<iso timestamp> <level> <pass|fail>` lines; malformed lines are skipped. */
export function parseCheckHistory(text) {
  const runs = []
  for (const line of text.split('\n')) {
    const m = /^(\S+)\s+(\S+)\s+(pass|fail)\s*$/.exec(line)
    if (m) runs.push({ at: m[1], level: m[2], passed: m[3] === 'pass' })
  }
  return runs
}

export function gateFailureRate(runs) {
  const byLevel = new Map()
  for (const run of runs) {
    const row = byLevel.get(run.level) ?? { level: run.level, runs: 0, failures: 0 }
    row.runs++
    if (!run.passed) row.failures++
    byLevel.set(run.level, row)
  }
  return [...byLevel.values()].sort((a, b) => a.level.localeCompare(b.level))
}

export function costOf(usage, model) {
  const price = PRICES[model] ?? DEFAULT_PRICE
  return (
    ((usage.input ?? 0) * price.input +
      (usage.output ?? 0) * price.output +
      (usage.cacheRead ?? 0) * price.cacheRead +
      (usage.cacheCreation ?? 0) * price.cacheCreation) /
    1_000_000
  )
}

/**
 * Sums usage per ticket from parsed transcript entries (`{ gitBranch, message: { id, model, usage } }`).
 * Claude Code writes one line per content block, so entries are deduplicated by message id.
 * Entries whose branch carries no issue number are counted under `unmapped`.
 */
export function tokensPerTicket(entries) {
  const seen = new Set()
  const tickets = new Map()
  for (const entry of entries) {
    const usage = entry.message?.usage
    if (!usage) continue
    const id = entry.message.id
    if (id) {
      if (seen.has(id)) continue
      seen.add(id)
    }
    const key = branchToTicket(entry.gitBranch) ?? 'unmapped'
    const row = tickets.get(key) ?? { ticket: key, input: 0, output: 0, cacheRead: 0, cacheCreation: 0, cost: 0 }
    const tokens = {
      input: usage.input_tokens ?? 0,
      output: usage.output_tokens ?? 0,
      cacheRead: usage.cache_read_input_tokens ?? 0,
      cacheCreation: usage.cache_creation_input_tokens ?? 0,
    }
    for (const k of Object.keys(tokens)) row[k] += tokens[k]
    row.cost += costOf(tokens, entry.message.model)
    tickets.set(key, row)
  }
  return [...tickets.values()].sort((a, b) => (a.ticket === 'unmapped') - (b.ticket === 'unmapped') || a.ticket - b.ticket)
}

const fmtHours = (h) => (h === null ? 'n/a' : h < 48 ? `${h.toFixed(1)} h` : `${(h / 24).toFixed(1)} d`)
const fmtNum = (n) => n.toLocaleString('en-US')

export function renderReport({ generatedAt, throughput, cycle, rework, gate, tokens }) {
  const out = [`# Delivery metrics`, '', `Snapshot generated ${generatedAt.slice(0, 10)} by \`scripts/delivery-metrics.mjs\`. Definitions and blind spots: [metrics-definitions](metrics-definitions.md).`, '']
  out.push('## Throughput: PRs merged per week', '', '| Week of | Merged |', '|---|---|')
  for (const r of throughput) out.push(`| ${r.week} | ${r.merged} |`)
  out.push('', '## Cycle time', '', '| Span | n | Median | p90 |', '|---|---|---|---|')
  out.push(`| Issue opened to merged | ${cycle.issueToMerged.n} | ${fmtHours(cycle.issueToMerged.median)} | ${fmtHours(cycle.issueToMerged.p90)} |`)
  out.push(`| PR opened to merged | ${cycle.prToMerged.n} | ${fmtHours(cycle.prToMerged.median)} | ${fmtHours(cycle.prToMerged.p90)} |`, '')
  const total = (key) => rework.reduce((s, r) => s + r[key], 0)
  out.push('## Rework per ticket', '', `${rework.length} tickets: ${total('commitsAfterOpen')} commits pushed after the PR opened, ${total('followUpFixes')} follow-up fix issues.`, '')
  out.push('| Ticket | Commits after PR opened | Follow-up fixes |', '|---|---|---|')
  for (const r of rework) out.push(`| #${r.ticket} | ${r.commitsAfterOpen} | ${r.followUpFixes} |`)
  out.push('', '## Gate-failure rate', '')
  if (gate === null) out.push('n/a (no `.git/check-history.log` on this machine).')
  else {
    out.push('| Level | Runs | Failures | Rate |', '|---|---|---|---|')
    for (const r of gate) out.push(`| ${r.level} | ${r.runs} | ${r.failures} | ${((r.failures / r.runs) * 100).toFixed(0)}% |`)
  }
  out.push('', '## Tokens and estimated cost per ticket', '')
  if (tokens === null) out.push('Skipped: `~/.claude/projects` not found on this machine.')
  else {
    out.push('| Ticket | Input | Output | Cache read | Cache creation | Est. cost (USD) |', '|---|---|---|---|---|---|')
    for (const r of tokens) {
      const label = r.ticket === 'unmapped' ? 'unmapped' : `#${r.ticket}`
      out.push(`| ${label} | ${fmtNum(r.input)} | ${fmtNum(r.output)} | ${fmtNum(r.cacheRead)} | ${fmtNum(r.cacheCreation)} | ${r.cost.toFixed(2)} |`)
    }
  }
  return out.join('\n') + '\n'
}

// ---- I/O ----

const gh = (...args) => JSON.parse(execFileSync('gh', ['api', '--paginate', '--slurp', ...args], { encoding: 'utf8', maxBuffer: 1 << 28 })).flat()

function fetchGitHub() {
  const pulls = gh('repos/{owner}/{repo}/pulls?state=closed&per_page=100').filter((p) => p.merged_at)
  const issues = gh('repos/{owner}/{repo}/issues?state=all&per_page=100').filter((i) => !i.pull_request)
  const issuesByNumber = new Map(issues.map((i) => [i.number, { createdAt: i.created_at }]))
  const prs = pulls.map((p) => {
    const commits = gh(`repos/{owner}/{repo}/pulls/${p.number}/commits?per_page=100`).map((c) => ({ committedDate: c.commit.committer.date }))
    const closing = [...`${p.body ?? ''}`.matchAll(/\bCloses #(\d+)/gi)].map((m) => Number(m[1]))
    return { number: p.number, createdAt: p.created_at, mergedAt: p.merged_at, closingIssues: closing, commits }
  })
  const fixIssues = issues.filter((i) => /^fix(\(|:|!)/.test(i.title)).map((i) => ({ number: i.number, title: i.title, body: i.body }))
  return { prs, issuesByNumber, fixIssues }
}

function readHistory() {
  const common = execFileSync('git', ['rev-parse', '--path-format=absolute', '--git-common-dir'], { encoding: 'utf8' }).trim()
  const file = path.join(common, 'check-history.log')
  return existsSync(file) ? parseCheckHistory(readFileSync(file, 'utf8')) : null
}

function* jsonlFiles(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) yield* jsonlFiles(full)
    else if (entry.name.endsWith('.jsonl')) yield full
  }
}

function readTranscripts() {
  const dir = path.join(homedir(), '.claude', 'projects')
  if (!existsSync(dir)) return null
  const entries = []
  for (const file of jsonlFiles(dir)) {
    for (const line of readFileSync(file, 'utf8').split('\n')) {
      if (!line.includes('"usage"')) continue
      try {
        entries.push(JSON.parse(line))
      } catch {
        // a partially written trailing line
      }
    }
  }
  return tokensPerTicket(entries)
}

function main() {
  const outIndex = process.argv.indexOf('--out')
  const { prs, issuesByNumber, fixIssues } = fetchGitHub()
  const history = readHistory()
  const report = renderReport({
    generatedAt: new Date().toISOString(),
    throughput: throughputPerWeek(prs),
    cycle: cycleTimes(prs, issuesByNumber),
    rework: reworkPerTicket(prs, fixIssues),
    gate: history && gateFailureRate(history),
    tokens: readTranscripts(),
  })
  if (outIndex > -1) writeFileSync(process.argv[outIndex + 1], report)
  else process.stdout.write(report)
}

if (process.argv[1] === fileURLToPath(import.meta.url)) main()
