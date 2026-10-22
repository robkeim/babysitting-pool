import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { formatCredits, formatInstant } from '@/features/credits'
import { useMyRequests } from './api'

/** "My Requests": everything the Member posted, with its state. Only the owner sees the exact address. */
export function MyRequestsPage() {
  const requests = useMyRequests()

  if (requests.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>We couldn&apos;t load your requests</AlertTitle>
        <AlertDescription>Please try again in a moment.</AlertDescription>
      </Alert>
    )
  }
  if (!requests.data) return null

  const items = requests.data.pages.flatMap((page) => page.items)
  if (items.length === 0) return <p>You haven&apos;t posted any requests yet.</p>

  return (
    <div className="space-y-4">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Starts</TableHead>
            <TableHead>Area and address</TableHead>
            <TableHead>State</TableHead>
            <TableHead className="text-right">Credits</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map((request) => (
            <TableRow key={request.id}>
              <TableCell>{formatInstant(request.startsAt)}</TableCell>
              <TableCell>
                {request.area}
                <br />
                <span className="text-muted-foreground">{request.address}</span>
              </TableCell>
              <TableCell>{request.status}</TableCell>
              <TableCell className="text-right">
                {formatCredits(request.costQuarterCredits)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      {requests.hasNextPage && (
        <Button
          variant="outline"
          onClick={() => void requests.fetchNextPage()}
          disabled={requests.isFetchingNextPage}
        >
          Load more
        </Button>
      )}
    </div>
  )
}
