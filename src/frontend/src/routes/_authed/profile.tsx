import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { PageHeader } from '@/components/PageHeader'
import { ColorSchemeCard, LanguageCard } from '@/features/auth'
import { ProfilePage } from '@/features/profile'

export const Route = createFileRoute('/_authed/profile')({
  component: function Profile() {
    return (
      <div className="space-y-4">
        <PageHeader>
          <FormattedMessage id="auth.accountMenu.profile" />
        </PageHeader>
        <ProfilePage />
        <LanguageCard />
        <ColorSchemeCard />
      </div>
    )
  },
})
