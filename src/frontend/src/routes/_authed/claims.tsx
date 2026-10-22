import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { MyClaimsPage } from '@/features/requests'

export const Route = createFileRoute('/_authed/claims')({
  component: function MyClaims() {
    return (
      <div className="space-y-4">
        <h2 className="text-xl font-semibold">
          <FormattedMessage id="requests.claims.title" />
        </h2>
        <MyClaimsPage />
      </div>
    )
  },
})
