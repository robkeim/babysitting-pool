import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { PageHeader } from '@/components/PageHeader'
import { InvitationsAdminPage } from '@/features/invitations'

export const Route = createFileRoute('/_authed/admin/invitations')({
  component: function InvitationsAdmin() {
    return (
      <div className="space-y-4">
        <PageHeader>
          <FormattedMessage id="invitations.admin.title" />
        </PageHeader>
        <InvitationsAdminPage />
      </div>
    )
  },
})
