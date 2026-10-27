import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { PageHeader } from '@/components/PageHeader'
import { MemberDetailPage } from '@/features/members'

export const Route = createFileRoute('/_authed/admin/members/$memberId')({
  component: function MemberDetail() {
    const { memberId } = Route.useParams()
    return (
      <div className="space-y-4">
        <PageHeader>
          <FormattedMessage id="members.admin.detail.title" />
        </PageHeader>
        <MemberDetailPage memberId={memberId} />
      </div>
    )
  },
})
