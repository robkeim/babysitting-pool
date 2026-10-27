import { Link } from '@tanstack/react-router'
import { Users } from 'lucide-react'
import { FormattedMessage } from 'react-intl'
import { EmptyState } from '@/components/EmptyState'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { formatCredits } from '@/features/credits'
import { useMembers } from './api'

/** The Admin's read-only list of every Member, closed ones included (spec §2A). */
export function MembersAdminPage() {
  const members = useMembers()

  if (members.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          <FormattedMessage id="members.admin.loadFailed.title" />
        </AlertTitle>
        <AlertDescription>
          <FormattedMessage id="members.admin.loadFailed.body" />
        </AlertDescription>
      </Alert>
    )
  }
  if (!members.data) return null

  const items = members.data.pages.flatMap((page) => page.items)
  return (
    <div className="space-y-4">
      {items.length === 0 && (
        <EmptyState icon={Users}>
          <FormattedMessage id="members.admin.empty" />
        </EmptyState>
      )}
      {items.map((member) => (
        <Card key={member.id} role="group" aria-labelledby={`member-${member.id}`}>
          <CardContent className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div className="min-w-0 space-y-1">
              <p className="font-medium [overflow-wrap:anywhere]">
                <Link
                  id={`member-${member.id}`}
                  to="/admin/members/$memberId"
                  params={{ memberId: member.id }}
                  className="underline-offset-4 hover:underline"
                >
                  {member.displayName}
                </Link>
                {member.isAdmin && (
                  <span className="ml-2 text-sm text-muted-foreground">
                    <FormattedMessage id="invitations.admin.admin" />
                  </span>
                )}
                {member.closedAt && (
                  <span className="ml-2 text-sm text-muted-foreground">
                    <FormattedMessage id="members.admin.closed" />
                  </span>
                )}
              </p>
              <p className="text-sm [overflow-wrap:anywhere] text-muted-foreground">
                {member.email}
              </p>
            </div>
            <p className="text-sm tabular-nums">
              <FormattedMessage
                id="members.admin.balances"
                values={{
                  available: formatCredits(member.availableQuarterCredits),
                  escrowed: formatCredits(member.escrowedQuarterCredits),
                }}
              />
            </p>
          </CardContent>
        </Card>
      ))}
      {members.hasNextPage && (
        <Button
          variant="outline"
          className="h-11 w-full lg:h-8 lg:w-auto"
          onClick={() => void members.fetchNextPage()}
          disabled={members.isFetchingNextPage}
        >
          <FormattedMessage id="credits.loadMore" />
        </Button>
      )}
    </div>
  )
}
