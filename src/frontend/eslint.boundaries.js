// Dependencies run one way: routes -> features -> shared (ADR 0005).
const deepFeatureImport = {
  group: ['@/features/*/*'],
  message: "Import another feature through its index.ts (ADR 0005), e.g. '@/features/credits'.",
}
const routesImport = {
  group: ['@/routes', '@/routes/*'],
  message: 'Only routes may depend on routes (ADR 0005).',
}
const featuresImport = {
  group: ['@/features', '@/features/*'],
  message: 'Shared code must not depend on features (ADR 0005).',
}

export const boundaryConfigs = [
  {
    files: ['src/features/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [deepFeatureImport, routesImport] }],
    },
  },
  {
    files: ['src/{lib,components,hooks}/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [routesImport, featuresImport] }],
    },
  },
  {
    files: ['src/routes/**/*.{ts,tsx}'],
    rules: {
      'react-refresh/only-export-components': 'off',
      'no-restricted-imports': ['error', { patterns: [deepFeatureImport] }],
    },
  },
]
