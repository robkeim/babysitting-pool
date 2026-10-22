import { ESLint } from 'eslint'
import tseslint from 'typescript-eslint'
import { describe, expect, it } from 'vitest'
import { boundaryConfigs } from '../../eslint.boundaries.js'

// Guards the import-boundary rules (ADR 0005) so they cannot silently stop working.
const eslint = new ESLint({
  overrideConfigFile: true,
  overrideConfig: [{ languageOptions: { parser: tseslint.parser } }, ...boundaryConfigs],
})

async function messages(filePath: string, source: string) {
  const [result] = await eslint.lintText(source, { filePath })
  return result?.messages.map((message) => message.message) ?? []
}

describe('import boundaries', () => {
  it('stops a feature importing from routes', async () => {
    expect(await messages('src/features/credits/a.ts', "import '@/routes/credits'")).toHaveLength(1)
  })

  it("stops a feature importing another feature's internals", async () => {
    expect(
      await messages('src/features/credits/a.ts', "import '@/features/members/api'"),
    ).toHaveLength(1)
  })

  it('allows a feature to use another feature through its index', async () => {
    expect(await messages('src/features/credits/a.ts', "import '@/features/members'")).toHaveLength(
      0,
    )
  })

  it('stops shared code importing a feature', async () => {
    expect(await messages('src/lib/a.ts', "import '@/features/credits'")).toHaveLength(1)
  })

  it('allows a route to import a feature through its index only', async () => {
    expect(await messages('src/routes/a.tsx', "import '@/features/credits'")).toHaveLength(0)
    expect(await messages('src/routes/a.tsx', "import '@/features/credits/api'")).toHaveLength(1)
  })
})
