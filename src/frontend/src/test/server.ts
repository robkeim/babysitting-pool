import { setupServer } from 'msw/node'

/** Shared MSW server; each feature contributes handlers from its own `mocks.ts`. */
export const server = setupServer()
