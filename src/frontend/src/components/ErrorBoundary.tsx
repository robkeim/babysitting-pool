import { Component, type ReactNode } from 'react'
import { FormattedMessage } from 'react-intl'
import { Button } from '@/components/ui/button'
import { logger } from '@/lib/logger'

interface Props {
  children: ReactNode
}

/** Catches a render error below it, reports it once to the logger (ADR 0010) and offers a reload. */
export class ErrorBoundary extends Component<Props, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  componentDidCatch(error: Error) {
    logger.reportError(error, { code: 'render_error' })
  }

  render() {
    if (!this.state.failed) return this.props.children
    return (
      <div role="alert" className="flex flex-col items-center gap-4 p-8 text-center">
        <p>
          <FormattedMessage id="common.somethingWentWrong" />
        </p>
        <Button
          onClick={() => {
            window.location.reload()
          }}
        >
          <FormattedMessage id="common.reload" />
        </Button>
      </div>
    )
  }
}
