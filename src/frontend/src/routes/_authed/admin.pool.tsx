import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { PageHeader } from '@/components/PageHeader'
import { PoolAdminPage } from '@/features/members'

export const Route = createFileRoute('/_authed/admin/pool')({
  component: function PoolAdmin() {
    return (
      <div className="space-y-4">
        <PageHeader>
          <FormattedMessage id="members.pool.title" />
        </PageHeader>
        <PoolAdminPage />
      </div>
    )
  },
})
