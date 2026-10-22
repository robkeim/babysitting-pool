import path from 'node:path'

const repositoryRoot = path.resolve(import.meta.dirname, '../../..')

export const FRONTEND_ROOT = path.resolve(import.meta.dirname, '..')
export const API_PROJECT = path.join(repositoryRoot, 'src/backend/BabysittingPool.Api')
export const API_DLL = path.join(API_PROJECT, 'bin/Debug/net10.0/BabysittingPool.Api.dll')
