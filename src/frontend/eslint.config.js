import js from '@eslint/js'
import globals from 'globals'
import formatjs from 'eslint-plugin-formatjs'
import jsxA11y from 'eslint-plugin-jsx-a11y'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import { defineConfig } from 'eslint/config'
import tseslint from 'typescript-eslint'

import { boundaryConfigs } from './eslint.boundaries.js'

export default defineConfig(
  {
    ignores: [
      'dist',
      'test-results',
      'playwright-report',
      'src/routeTree.gen.ts',
      'src/**/*.gen.ts',
      'src/components/ui',
    ],
  },
  js.configs.recommended,
  tseslint.configs.strictTypeChecked,
  tseslint.configs.stylisticTypeChecked,
  jsxA11y.flatConfigs.recommended,
  {
    languageOptions: {
      globals: { ...globals.browser },
      parserOptions: {
        projectService: { allowDefaultProject: ['eslint.config.js', 'eslint.boundaries.js'] },
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: { 'react-hooks': reactHooks, 'react-refresh': reactRefresh },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['error', { allowConstantExport: true }],
      '@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
    },
  },
  ...boundaryConfigs,
  {
    // User-visible text lives in the catalogs (ADR 0009).
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['**/*.test.{ts,tsx}', 'src/test/**'],
    plugins: { formatjs },
    rules: {
      'formatjs/no-literal-string-in-jsx': 'error',
      'formatjs/no-literal-string-in-object': 'error',
    },
  },
  {
    files: ['**/*.{test,spec}.{ts,tsx}', 'src/test/**', 'e2e/**'],
    rules: { 'react-refresh/only-export-components': 'off' },
  },
  {
    files: ['**/*.{js,mjs}'],
    extends: [tseslint.configs.disableTypeChecked],
  },
  {
    files: ['*.config.{js,ts}', 'e2e/**', 'scripts/**'],
    languageOptions: { globals: { ...globals.node } },
  },
)
