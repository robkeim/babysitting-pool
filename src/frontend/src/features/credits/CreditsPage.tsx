import { Button } from '@/components/ui/button'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useMyAccount } from './api'
import { formatCredits, formatInstant } from './format'

export function CreditsPage() {
  const account = useMyAccount()

  if (account.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>We couldn't load your credits</AlertTitle>
        <AlertDescription>Please try again in a moment.</AlertDescription>
      </Alert>
    )
  }
  if (!account.data) return null

  const [first] = account.data.pages
  if (!first) return null
  const entries = account.data.pages.flatMap((page) => page.ledger.items)

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2">
        <Card role="group" aria-label="Available">
          <CardHeader>
            <CardTitle>Available</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">
            {formatCredits(first.availableQuarterCredits)}
          </CardContent>
        </Card>
        <Card role="group" aria-label="In escrow">
          <CardHeader>
            <CardTitle>In escrow</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">
            {formatCredits(first.escrowedQuarterCredits)}
          </CardContent>
        </Card>
      </div>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>When</TableHead>
            <TableHead>Kind</TableHead>
            <TableHead className="text-right">Credits</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {entries.map((entry) => (
            <TableRow key={entry.id}>
              <TableCell>{formatInstant(entry.occurredAt)}</TableCell>
              <TableCell>{entry.kind}</TableCell>
              <TableCell className="text-right">
                {formatCredits(entry.amountQuarterCredits)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      {account.hasNextPage && (
        <Button
          variant="outline"
          onClick={() => void account.fetchNextPage()}
          disabled={account.isFetchingNextPage}
        >
          Load more
        </Button>
      )}
    </div>
  )
}
