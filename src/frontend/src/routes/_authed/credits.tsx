import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { PageHeader } from '@/components/PageHeader'
import { CreditsPage } from '@/features/credits'

export const Route = createFileRoute('/_authed/credits')({
  component: function Credits() {
    return (
      <div className="space-y-4">
        <PageHeader>
          <FormattedMessage id="common.nav.credits" />
        </PageHeader>
        <CreditsPage />
      </div>
    )
  },
})
