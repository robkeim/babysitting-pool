import { createFileRoute, Link } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { PageHeader } from '@/components/PageHeader'
import { MyRequestsPage } from '@/features/requests'
import { buttonVariants } from '@/components/ui/button'

export const Route = createFileRoute('/_authed/requests/')({
  component: function MyRequests() {
    return (
      <div className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <PageHeader>
            <FormattedMessage id="requests.myRequests.title" />
          </PageHeader>
          <Link to="/requests/new" className={buttonVariants({ className: 'h-10 lg:h-8' })}>
            <FormattedMessage id="requests.myRequests.post" />
          </Link>
        </div>
        <MyRequestsPage />
      </div>
    )
  },
})
