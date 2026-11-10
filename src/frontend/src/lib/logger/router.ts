import type { AnyRouter } from '@tanstack/react-router'
import { setRouteName } from './index'

/** Keeps the logger's route name current: the route's id (`/_authed/requests/$requestId`), never the concrete path. */
export function trackRouteName(router: AnyRouter) {
  return router.subscribe('onResolved', () => {
    setRouteName(router.state.matches.at(-1)?.routeId ?? 'unknown')
  })
}
