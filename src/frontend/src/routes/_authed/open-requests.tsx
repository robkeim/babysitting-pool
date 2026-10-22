import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { OpenRequestsPage } from '@/features/requests'

export const Route = createFileRoute('/_authed/open-requests')({
  component: function OpenRequests() {
    return (
      <div className="space-y-4">
        <h2 className="text-xl font-semibold">
          <FormattedMessage id="requests.open.title" />
        </h2>
        <OpenRequestsPage />
      </div>
    )
  },
})
