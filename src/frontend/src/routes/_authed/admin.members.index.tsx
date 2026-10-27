import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { PageHeader } from '@/components/PageHeader'
import { MembersAdminPage } from '@/features/members'

export const Route = createFileRoute('/_authed/admin/members/')({
  component: function MembersAdmin() {
    return (
      <div className="space-y-4">
        <PageHeader>
          <FormattedMessage id="members.admin.title" />
        </PageHeader>
        <MembersAdminPage />
      </div>
    )
  },
})
