# Claude Code with visuals

Research for issue #15 (map #6). Question: how do Claude Code and agents work with visual design in practice (screenshot/Playwright MCP verification, mockups or images as input, design-system/Storybook tooling, known limits)?

Sources are official docs, fetched 2026-10-21. The decision belongs to the design-loop ticket (#16).

## 1. Images and mockups as input

- Claude Code accepts images by drag and drop, `Ctrl+V` paste, or a file path in the prompt. Multiple images per conversation work. Documented prompts include "Generate CSS to match this design mockup" and "Describe the UI elements in this screenshot". [Common workflows: Work with images](https://code.claude.com/docs/en/common-workflows)
- The best-practices doc recommends this loop for UI: "[paste screenshot] implement this design. take a screenshot of the result and compare it to the original. list differences and fix them". [Best practices](https://code.claude.com/docs/en/best-practices)
- Model-side limits (API vision docs): JPEG/PNG/GIF/WebP only (GIF uses first frame); max 8000x8000 px; 10 MB per image on the Claude API; images above the model's native resolution are downscaled (standard tier 1568 px long edge / 1568 visual tokens; Claude 4.7+ models 2576 px / 4784 tokens). Cost is `ceil(w/28) x ceil(h/28)` visual tokens. Above 20 images per request a 2000 px per-side limit applies. [Vision](https://platform.claude.com/docs/en/build-with-claude/vision)
- Documented limitations: may hallucinate or err on low-quality, rotated, or very small (<200 px) images; coordinate/localization output is approximate; counting is approximate; downscaling can make small text illegible; Claude cannot generate or edit images. "Always carefully review and verify Claude's image interpretations." [Vision: Limitations](https://platform.claude.com/docs/en/build-with-claude/vision)
- Implication (inference): good for layout and hierarchy comparison against a mockup, not a pixel-diff tool. Exact regressions need a deterministic tool (section 4).

## 2. Browser-driven verification

### Playwright MCP
- Browser automation over MCP. Primary perception is the accessibility tree: structured snapshots with roles, text and ref IDs (`ref=e5`), "no vision models required". Screenshots are a separate capability for verifying the page or an element. [Playwright MCP](https://playwright.dev/docs/getting-started-mcp)
- Claude Code install: `claude mcp add playwright npx @playwright/mcp@latest` (Node 20+). Headed by default; `--headless`, browser choice (chrome/firefox/webkit/msedge), isolated/persistent profiles. Also exposes console messages, network listing and route mocking, tab and dialog handling.
- Cautions: `browser_run_code_unsafe` is RCE-equivalent; page-supplied WebMCP tool data is untrusted; headless browsers close after an hour idle by default.
- Claude Code MCP output: warns above 10,000 tokens, caps at 25,000 by default (`MAX_MCP_OUTPUT_TOKENS`); tools returning image data remain subject to that limit. [Claude Code MCP](https://code.claude.com/docs/en/mcp)

### Claude in Chrome (first-party)
- `claude --chrome` connects Claude Code to the Claude in Chrome extension. Documented capabilities: live debugging (console, DOM), "Design verification: build a UI from a Figma mock, then open it in the browser to verify it matches", visual-regression checks, flow testing, GIF recording, saving screenshots to disk. [Use Claude Code with Chrome](https://code.claude.com/docs/en/chrome)
- Constraints: Chromium browser plus extension v1.0.36+, a direct Anthropic plan (Pro/Max/Team/Enterprise) with `/login` auth (not API key, not Bedrock/Vertex/Foundry), no WSL. Pauses on login/CAPTCHA. Modal JS dialogs block it; the extension service worker can idle and need `/chrome` reconnect. Enabling by default adds always-loaded tool context. It drives the user's real visible browser, so it is interactive, not a headless CI option (inference from the doc).

### General principle
- "Give Claude a check it can run: tests, a build, a screenshot to compare." Have Claude show evidence (a screenshot) rather than assert success. A Stop hook or `/goal` can make the check a hard gate; a verification subagent avoids the writer grading itself. [Best practices](https://code.claude.com/docs/en/best-practices)

## 3. Design-system and component tooling

- **shadcn MCP** (matches this repo's stack): `pnpm dlx shadcn@latest mcp init --client claude`, or `npx shadcn@latest mcp` in `.mcp.json`. Agents browse, search and install components/blocks from registries in `components.json`, including private and third-party registries. It supplies components, not visual judgement. [shadcn/ui MCP](https://ui.shadcn.com/docs/mcp)
- **Storybook MCP**: lets agents look up components and docs (props, examples), generate and preview stories, and run story tests including accessibility checks, with a fix-and-rerun loop. Works in any Storybook project; the docs toolset needs a components manifest (off by default; React frameworks supported). Storybook says its AI capabilities are "in preview" and "The API may change in future releases." Setup includes adding instructions to AGENTS.md/CLAUDE.md. [Storybook MCP overview](https://storybook.js.org/docs/ai/mcp/overview)
- **Figma MCP**: remote server `https://mcp.figma.com/mcp`: design context (variables, components, layout), code generation from frames, Code Connect mapping to existing components, canvas writing (beta, free during beta then usage-based), live capture of localhost into Figma layers. Needs a Figma plan (Starter and up); desktop server needs a Dev/Full seat. Rate limits are not stated on the page. [Guide to the Figma MCP server](https://help.figma.com/hc/en-us/articles/32132100833559-Guide-to-the-Figma-MCP-server)
- This project has no stated Figma file, so Figma is optional.

## 4. Deterministic visual regression

- Playwright Test `expect(page).toHaveScreenshot()` writes a baseline on first run and compares afterwards; baselines are per browser and platform (e.g. `chromium-darwin`). Rendering varies by "host OS, version, settings, hardware", so generate and compare in the same environment. Tunables: `threshold` (default 0.2), `maxDiffPixels`, `stylePath` to hide volatile elements. Update with `npx playwright test --update-snapshots`; commit and review snapshot diffs. [Playwright visual comparisons](https://playwright.dev/docs/test-snapshots)
- Storybook's cloud option is Chromatic: every story is snapshotted against a baseline; a cloud service from the Storybook team. [Storybook visual testing](https://storybook.js.org/docs/writing-tests/visual-testing)
- These give pass/fail signals an agent can act on, unlike a subjective "looks right".

## 5. Known limits, summarized

| Limit | Source |
|---|---|
| Vision is approximate on spatial reasoning, small text, counting; must be verified | Vision doc |
| Large screenshots are downscaled (small text lost); up to ~4.8k tokens each | Vision doc |
| MCP output capped at 25k tokens by default; images count | Claude Code MCP doc |
| Context fills fast and performance degrades; use subagents for exploration | Best practices |
| Claude in Chrome: interactive, real Chrome, direct Anthropic plan, no WSL | Chrome doc |
| Playwright screenshot baselines are OS/browser specific | Playwright docs |
| Storybook MCP is preview, API may change | Storybook docs |
| Figma MCP needs a Figma plan; canvas writing is beta | Figma help |

## 6. Candidate recommendation (non-binding, for #16)

1. Primary verify loop: Playwright MCP (accessibility snapshots for interaction, targeted screenshots for visual checks) against the dev server, with a mockup or reference screenshot as input. Pin it in `.mcp.json`; document the loop and mobile and desktop viewports in AGENTS.md.
2. Add shadcn MCP for component discovery; fits the stack, low risk.
3. Deterministic gate: Playwright `toHaveScreenshot` on a few key pages, baselines generated on the CI OS only, once the first vertical slice exists. Defer Chromatic.
4. Storybook and its MCP: optional; preview status and extra maintenance. Revisit if component count grows. Figma only if a design file appears.
5. Claude in Chrome for ad-hoc human-in-the-loop checks, not the automated loop.
6. Keep screenshots scoped (element or viewport) to protect context and tokens; require screenshot evidence in PRs.

## Open questions for #16
- Which viewports and themes (dark mode, EN/FR text length) must be screenshot-verified?
- Is a pixel-regression suite worth the baseline churn at this stage?
- Where do mockups live (repo `docs/design/`, Figma, or none)?
