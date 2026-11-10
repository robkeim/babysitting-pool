import assert from 'node:assert/strict'
import { test } from 'node:test'
import {
  branchToTicket,
  costOf,
  cycleTimes,
  gateFailureRate,
  parseCheckHistory,
  percentile,
  reworkPerTicket,
  throughputPerWeek,
  tokensPerTicket,
  weekStart,
} from './delivery-metrics.mjs'

test('branchToTicket maps <issue>-<slug> with or without a prefix', () => {
  assert.equal(branchToTicket('283-delivery-metrics'), 283)
  assert.equal(branchToTicket('robkeim/283-delivery-metrics'), 283)
  assert.equal(branchToTicket('robkeim/chore-metrics'), null)
  assert.equal(branchToTicket('main'), null)
  assert.equal(branchToTicket(undefined), null)
})

test('percentile interpolates and handles empty input', () => {
  assert.equal(percentile([], 0.5), null)
  assert.equal(percentile([4, 1, 3, 2], 0.5), 2.5)
  assert.equal(percentile([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], 0.9), 9.1)
})

test('weekStart is the UTC Monday', () => {
  assert.equal(weekStart('2026-11-08T23:00:00Z'), '2026-11-02') // Sunday
  assert.equal(weekStart('2026-11-09T00:00:00Z'), '2026-11-09') // Monday
})

test('throughputPerWeek counts merged PRs per week, ignoring unmerged', () => {
  const prs = [
    { mergedAt: '2026-11-02T10:00:00Z' },
    { mergedAt: '2026-11-04T10:00:00Z' },
    { mergedAt: '2026-11-10T10:00:00Z' },
    { mergedAt: null },
  ]
  assert.deepEqual(throughputPerWeek(prs), [
    { week: '2026-11-02', merged: 2 },
    { week: '2026-11-09', merged: 1 },
  ])
})

test('cycleTimes measures issue and PR spans in hours', () => {
  const prs = [
    { createdAt: '2026-11-01T10:00:00Z', mergedAt: '2026-11-01T12:00:00Z', closingIssues: [1] },
    { createdAt: '2026-11-02T00:00:00Z', mergedAt: '2026-11-02T04:00:00Z', closingIssues: [] },
  ]
  const issues = new Map([[1, { createdAt: '2026-11-01T06:00:00Z' }]])
  const { issueToMerged, prToMerged } = cycleTimes(prs, issues)
  assert.deepEqual(issueToMerged, { n: 1, median: 6, p90: 6 })
  assert.equal(prToMerged.n, 2)
  assert.equal(prToMerged.median, 3)
})

test('reworkPerTicket counts commits after open and follow-up fix issues', () => {
  const prs = [
    {
      createdAt: '2026-11-01T10:00:00Z',
      mergedAt: '2026-11-02T10:00:00Z',
      closingIssues: [5],
      commits: [{ committedDate: '2026-11-01T09:00:00Z' }, { committedDate: '2026-11-01T11:00:00Z' }],
    },
  ]
  const fixes = [
    { number: 9, title: 'fix(x): regression from #5', body: '' },
    { number: 10, title: 'fix(x): unrelated', body: 'see #50' },
  ]
  assert.deepEqual(reworkPerTicket(prs, fixes), [{ ticket: 5, commitsAfterOpen: 1, followUpFixes: 1 }])
})

test('parseCheckHistory skips malformed lines and gateFailureRate groups by level', () => {
  const runs = parseCheckHistory('2026-11-01T10:00:00Z fast pass\ngarbage\n2026-11-01T11:00:00Z fast fail\n2026-11-01T12:00:00Z full pass\n')
  assert.equal(runs.length, 3)
  assert.deepEqual(gateFailureRate(runs), [
    { level: 'fast', runs: 2, failures: 1 },
    { level: 'full', runs: 1, failures: 0 },
  ])
})

test('costOf uses the model price and falls back for unknown models', () => {
  const usage = { input: 1_000_000, output: 1_000_000, cacheRead: 0, cacheCreation: 0 }
  assert.equal(costOf(usage, 'claude-sonnet-5-5'), 18)
  assert.equal(costOf(usage, 'some-future-model'), 18)
})

test('tokensPerTicket sums per ticket, dedupes message ids and buckets unmapped branches', () => {
  const entry = (gitBranch, id, usage) => ({ gitBranch, message: { id, model: 'claude-sonnet-5-5', usage } })
  const u = { input_tokens: 10, output_tokens: 20, cache_read_input_tokens: 30, cache_creation_input_tokens: 40 }
  const rows = tokensPerTicket([
    entry('robkeim/7-thing', 'a', u),
    entry('robkeim/7-thing', 'a', u),
    entry('7-thing', 'b', u),
    entry('robkeim/no-number', 'c', u),
    { gitBranch: '7-thing', message: { id: 'd' } },
  ])
  assert.equal(rows.length, 2)
  assert.deepEqual(rows[0], { ticket: 7, input: 20, output: 40, cacheRead: 60, cacheCreation: 80, cost: rows[0].cost })
  assert.ok(rows[0].cost > 0)
  assert.equal(rows[1].ticket, 'unmapped')
})
