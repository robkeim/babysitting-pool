import { type ReactNode } from 'react'

/** The title at the top of a page. The wordmark in the shell is the page's `h1`, so this is an `h2`. */
export function PageHeader({ children }: { children: ReactNode }) {
  return <h2 className="text-xl font-semibold tracking-tight lg:text-2xl">{children}</h2>
}
