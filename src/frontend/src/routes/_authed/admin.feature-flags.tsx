import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { FeatureFlagsAdminPage } from '@/features/flags'

export const Route = createFileRoute('/_authed/admin/feature-flags')({
  component: function FeatureFlagsAdmin() {
    return (
      <div className="space-y-4">
        <h2 className="text-xl font-semibold">
          <FormattedMessage id="flags.admin.title" />
        </h2>
        <FeatureFlagsAdminPage />
      </div>
    )
  },
})
