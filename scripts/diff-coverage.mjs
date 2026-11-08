#!/usr/bin/env node
// Diff-coverage check (ADR 0021): of the lines this branch adds or changes, the share that is executable
// and covered by tests must reach the threshold. Lines that coverage does not track (comments, types,
// excluded files) are ignored.
//
//   node scripts/diff-coverage.mjs [--min 80] [--base <ref>] <coverage-file>...
//
// Reads Cobertura XML (backend) and lcov (frontend). DIFF_COVERAGE_MIN overrides the default threshold;
// DIFF_COVERAGE=off skips the check.
import { execFileSync } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'

const root = execFileSync('git', ['rev-parse', '--show-toplevel'], { encoding: 'utf8' }).trim()
const git = (...args) => execFileSync('git', args, { cwd: root, encoding: 'utf8', maxBuffer: 1 << 28 })

let min = Number(process.env.DIFF_COVERAGE_MIN ?? 80)
let base
const files = []
const argv = process.argv.slice(2)
for (let i = 0; i < argv.length; i++) {
  if (argv[i] === '--min') min = Number(argv[++i])
  else if (argv[i] === '--base') base = argv[++i]
  else files.push(argv[i])
}
if (process.env.DIFF_COVERAGE === 'off') {
  console.log('diff-coverage: skipped (DIFF_COVERAGE=off)')
  process.exit(0)
}
if (files.length === 0 || Number.isNaN(min)) {
  console.error('usage: diff-coverage.mjs [--min N] [--base ref] <cobertura.xml|lcov.info>...')
  process.exit(2)
}

// repo-relative path -> Map(line -> hits), merged across every report (a line is covered if any run hit it).
const coverage = new Map()
function record(file, line, hits) {
  const rel = path.relative(root, path.resolve(root, file)).split(path.sep).join('/')
  if (rel.startsWith('..')) return
  let lines = coverage.get(rel)
  if (!lines) coverage.set(rel, (lines = new Map()))
  lines.set(line, Math.max(lines.get(line) ?? 0, hits))
}

function readCobertura(file) {
  const xml = readFileSync(file, 'utf8')
  const sources = [...xml.matchAll(/<source>([^<]*)<\/source>/g)].map((m) => m[1])
  for (const cls of xml.matchAll(/<class\b[^>]*\bfilename="([^"]*)"[^>]*>([\s\S]*?)<\/class>/g)) {
    let filename = cls[1]
    if (!path.isAbsolute(filename)) {
      filename = sources.map((s) => path.join(s, filename)).find(existsSync) ?? filename
    }
    for (const line of cls[2].matchAll(/<line\b[^>]*\bnumber="(\d+)"[^>]*\bhits="(\d+)"/g)) {
      record(filename, Number(line[1]), Number(line[2]))
    }
  }
}

function readLcov(file) {
  const dir = path.dirname(path.dirname(path.resolve(file))) // <package>/coverage/lcov.info -> <package>
  let current
  for (const row of readFileSync(file, 'utf8').split('\n')) {
    if (row.startsWith('SF:')) current = path.resolve(dir, row.slice(3))
    else if (row.startsWith('DA:') && current) {
      const [line, hits] = row.slice(3).split(',')
      record(current, Number(line), Number(hits))
    } else if (row === 'end_of_record') current = undefined
  }
}

for (const file of files) {
  if (!existsSync(file)) {
    console.error(`diff-coverage: ${file} not found; did the tests run with coverage?`)
    process.exit(2)
  }
  if (file.endsWith('.xml')) readCobertura(file)
  else readLcov(file)
}

// Changed lines per file: the merge-base with main to the working tree, plus untracked files in full.
base ??= ['origin/main', 'main'].find((ref) => {
  try {
    git('rev-parse', '--verify', '--quiet', ref)
    return true
  } catch {
    return false
  }
})
if (!base) {
  console.error('diff-coverage: no origin/main or main to compare against')
  process.exit(2)
}
const mergeBase = git('merge-base', base, 'HEAD').trim()
const changed = new Map()
let file
for (const row of git('diff', '-U0', '--no-color', '--no-ext-diff', mergeBase).split('\n')) {
  if (row.startsWith('+++ ')) file = row === '+++ /dev/null' ? undefined : row.slice(6)
  const hunk = /^@@ -\S+ \+(\d+)(?:,(\d+))? @@/.exec(row)
  if (hunk && file) {
    const start = Number(hunk[1])
    const count = hunk[2] === undefined ? 1 : Number(hunk[2])
    const lines = changed.get(file) ?? new Set()
    for (let n = start; n < start + count; n++) lines.add(n)
    changed.set(file, lines)
  }
}
for (const untracked of git('ls-files', '--others', '--exclude-standard').split('\n').filter(Boolean)) {
  const count = readFileSync(path.join(root, untracked), 'utf8').split('\n').length
  changed.set(untracked, new Set(Array.from({ length: count }, (_, i) => i + 1)))
}

let coverable = 0
let covered = 0
const misses = []
for (const [name, lines] of changed) {
  const tracked = coverage.get(name)
  if (!tracked) continue
  const missed = [...lines].filter((n) => tracked.has(n) && tracked.get(n) === 0).sort((a, b) => a - b)
  const hit = [...lines].filter((n) => (tracked.get(n) ?? 0) > 0).length
  coverable += hit + missed.length
  covered += hit
  if (missed.length > 0) misses.push(`  ${name}: ${missed.join(', ')}`)
}

const pct = coverable === 0 ? 100 : (covered / coverable) * 100
console.log(`diff-coverage: ${covered}/${coverable} changed executable lines covered (${pct.toFixed(1)}%, minimum ${min}%)`)
if (misses.length > 0) console.log(`uncovered changed lines:\n${misses.join('\n')}`)
if (pct < min) {
  console.error('diff-coverage: below the minimum; add tests for the lines above (or set DIFF_COVERAGE=off to skip once).')
  process.exit(1)
}
