import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { PageHeader } from '@/components/PageHeader'
import { OpenRequestsPage } from '@/features/requests'

export const Route = createFileRoute('/_authed/open-requests')({
  component: function OpenRequests() {
    return (
      <div className="space-y-4">
        <PageHeader>
          <FormattedMessage id="requests.open.title" />
        </PageHeader>
        <OpenRequestsPage />
      </div>
    )
  },
})
