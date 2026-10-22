# Documentation has four layers with one owner each, and agents read selectively through a router, an index and read-first lines

Agents pay tokens for every file they open, and `AGENTS.md` is paid for in every session. So the docs are laid out to be skimmed before they are opened, and each fact lives in exactly one place.

## Layers

| Layer | Files | Job |
|---|---|---|
| Humans | `README.md` | What the project is and how to start; links to `docs/development.md`, repeats nothing |
| Agent entry | `AGENTS.md` | A router of at most 60 lines: stack in one line each, a read-first table, the definition of done. It never restates a rule that lives elsewhere |
| Rules | `docs/specs/`, `docs/adr/`, `GLOSSARY.md` | Business rules, decisions and vocabulary |
| How-to | `docs/development.md`, `docs/testing.md`, `docs/agents/` | Running, testing and working the tracker |

## Selective reading

- `docs/adr/README.md` indexes every ADR with one line and area tags (backend, frontend, ops, i18n and so on). Every ADR's first line is its decision statement.
- The spec stays one file and is cited by section number (§4). Split it by feature once it passes about 20 KB.
- `.claudeignore` is deleted: Claude Code does not read it. `Read(...)` deny rules in `.claude/settings.json` hide lockfiles, `schema.gen.ts`, the Postman collection, EF `Migrations/` and `docs/research/`. `docs/api/openapi.json` stays readable as the canonical contract, and agents grep it. Research notes stay in git for humans; an agent that needs one is told to by the ticket.

## Staying current

- `scripts/check-docs.sh` runs first in `scripts/check.sh` (so in hooks and CI): `AGENTS.md` ≤ 60 lines, every ADR listed in the index, every relative Markdown link resolves.
- The PR template has a checkbox: spec, ADRs and glossary updated if behaviour or a decision changed.
- ADRs are append-only: a new ADR supersedes (the old one is marked `Superseded by`), a small change is a dated amendment note (as in ADR 0008).
- Generated artifacts stay guarded by the existing CI drift checks (ADR 0006).
- No doc-gardening agent: deterministic checks first, as in ADR 0008.

## Tickets

One ticket is one vertical slice, one PR, small enough for one context window. Every `ready-for-agent` ticket carries a **Read first:** line naming the spec sections and ADRs it needs (for example `spec §2, §2A; ADR 0006`). A ticket that needs more than about 5 files of context is split. Bundling several slices in one ticket was rejected: context grows and gets compacted mid-task, PRs become hard to review, and parallel agents cannot split the work. Layer-by-layer tickets were rejected as well: nothing works end to end until the last one lands, which also contradicts the vertical-slice plan (#22).

## Budget

Soft targets, measured as characters ÷ 4 until a real `/context` reading replaces them: `AGENTS.md` ≤ 60 lines (enforced), and a typical task's read-first set ≤ about 8k tokens.

| Set | Size | Approx. tokens |
|---|---|---|
| `AGENTS.md` (loaded every session) | 2.8 KB | ~0.7k |
| `AGENTS.md` + whole spec + glossary (domain task, upper bound) | 19 KB | ~4.8k |
| `AGENTS.md` + index + 2 ADRs | 9.5 KB | ~2.4k |

Retune after the first feature slices land.
