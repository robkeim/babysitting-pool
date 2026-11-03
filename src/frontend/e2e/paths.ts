import path from 'node:path'

const repositoryRoot = path.resolve(import.meta.dirname, '../../..')

export const FRONTEND_ROOT = path.resolve(import.meta.dirname, '..')
/** The E2E host: the API plus the test-only endpoints, which Production does not contain (ADR 0016). */
export const API_PROJECT = path.join(repositoryRoot, 'src/backend/BabysittingPool.E2E')
export const API_DLL = path.join(API_PROJECT, 'bin/Debug/net10.0/BabysittingPool.E2E.dll')
