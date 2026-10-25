import { type LucideIcon } from 'lucide-react'
import { type ReactNode } from 'react'
import { Card, CardContent } from '@/components/ui/card'

/** What a list says when it has nothing to show: an icon and one sentence. */
export function EmptyState({ icon: Icon, children }: { icon: LucideIcon; children: ReactNode }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-3 py-8 text-center text-muted-foreground">
        <Icon className="size-8" aria-hidden />
        <p>{children}</p>
      </CardContent>
    </Card>
  )
}
