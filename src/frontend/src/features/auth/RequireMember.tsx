import type { ReactNode } from 'react'
import { isUnauthenticated, useMe } from './api'

interface RequireMemberProps {
  children: ReactNode
  /** Shown instead of the children when nobody is signed in. */
  signedOut: ReactNode
}

export function RequireMember({ children, signedOut }: RequireMemberProps) {
  const me = useMe()
  if (me.isError) return isUnauthenticated(me.error) ? signedOut : <p>Something went wrong.</p>
  if (!me.data) return null
  return children
}
