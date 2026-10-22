import { createFileRoute, Link } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { MyRequestsPage } from '@/features/requests'
import { buttonVariants } from '@/components/ui/button'

export const Route = createFileRoute('/_authed/requests/')({
  component: function MyRequests() {
    return (
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-semibold">
            <FormattedMessage id="requests.myRequests.title" />
          </h2>
          <Link to="/requests/new" className={buttonVariants()}>
            <FormattedMessage id="requests.myRequests.post" />
          </Link>
        </div>
        <MyRequestsPage />
      </div>
    )
  },
})
