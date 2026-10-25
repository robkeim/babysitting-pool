import { createFileRoute } from '@tanstack/react-router'
import { FormattedMessage } from 'react-intl'
import { PageHeader } from '@/components/PageHeader'
import { HowItWorksPage } from '@/features/rules'

export const Route = createFileRoute('/_authed/how-it-works')({
  component: function HowItWorks() {
    return (
      <div className="space-y-4">
        <PageHeader>
          <FormattedMessage id="rules.title" />
        </PageHeader>
        <HowItWorksPage />
      </div>
    )
  },
})
