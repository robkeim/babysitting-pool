// Errors go through the logger, the only module that may write to the console (ADR 0010); tests may spy on it.
export const loggingConfigs = [
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/lib/logger/logger.ts', '**/*.test.{ts,tsx}', 'src/test/**'],
    rules: { 'no-console': 'error' },
  },
]
