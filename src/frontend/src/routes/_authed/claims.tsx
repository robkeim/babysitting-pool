import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { PageHeader } from '@/components/PageHeader'
import { MyClaimsPage } from '@/features/requests'

export const Route = createFileRoute('/_authed/claims')({
  component: function MyClaims() {
    return (
      <div className="space-y-4">
        <PageHeader>
          <FormattedMessage id="requests.claims.title" />
        </PageHeader>
        <MyClaimsPage />
      </div>
    )
  },
})
