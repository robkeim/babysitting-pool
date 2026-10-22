import { createFileRoute, Link } from '@tanstack/react-router'
import { MyRequestsPage } from '@/features/requests'
import { buttonVariants } from '@/components/ui/button'

export const Route = createFileRoute('/_authed/requests/')({
  component: function MyRequests() {
    return (
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-semibold">My Requests</h2>
          <Link to="/requests/new" className={buttonVariants()}>
            Post a request
          </Link>
        </div>
        <MyRequestsPage />
      </div>
    )
  },
})
