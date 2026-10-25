import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { PageHeader } from '@/components/PageHeader'
import { FeatureFlagsAdminPage } from '@/features/flags'

export const Route = createFileRoute('/_authed/admin/feature-flags')({
  component: function FeatureFlagsAdmin() {
    return (
      <div className="space-y-4">
        <PageHeader>
          <FormattedMessage id="flags.admin.title" />
        </PageHeader>
        <FeatureFlagsAdminPage />
      </div>
    )
  },
})
