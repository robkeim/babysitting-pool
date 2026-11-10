import { ESLint } from 'eslint'
import tseslint from 'typescript-eslint'
import { describe, expect, it } from 'vitest'
import { loggingConfigs } from '../../eslint.logging.js'

// Guards the no-console rule (ADR 0010) so it cannot silently stop working.
const eslint = new ESLint({
  overrideConfigFile: true,
  overrideConfig: [{ languageOptions: { parser: tseslint.parser } }, ...loggingConfigs],
})

async function count(filePath: string, source: string) {
  const [result] = await eslint.lintText(source, { filePath })
  // A file no config matches yields an "ignored" warning with no rule id.
  return result?.messages.filter((message) => message.ruleId === 'no-console').length ?? 0
}

describe('no-console', () => {
  it('stops console use in app code', async () => {
    expect(await count('src/features/credits/a.ts', "console.log('x')")).toBe(1)
    expect(await count('src/lib/a.ts', "console.error('x')")).toBe(1)
  })

  it('allows it in tests, which spy on it', async () => {
    expect(await count('src/lib/a.test.ts', "console.error('x')")).toBe(0)
  })

  it('allows it in the logger module', async () => {
    expect(await count('src/lib/logger/logger.ts', "console.error('x')")).toBe(0)
  })
})
