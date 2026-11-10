import { Logger } from './logger'

export { installGlobalHandlers, sanitizeMessage, Logger, type ClientLog } from './logger'

let routeName = 'unknown'

/** The router calls this on navigation so reports carry the route's name, never its concrete path. */
export function setRouteName(name: string) {
  routeName = name
}

export function getRouteName() {
  return routeName
}

export const logger = new Logger({
  production: import.meta.env.PROD,
  appVersion: (import.meta.env.VITE_APP_VERSION as string | undefined) ?? 'dev',
  getRoute: getRouteName,
})
