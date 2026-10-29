import { Bell } from 'lucide-react'
import { useState } from 'react'
import { useIntl } from 'react-intl'
import { shellTabClass } from '@/components/AppShell'
import { Popover, PopoverContent, PopoverTitle, PopoverTrigger } from '@/components/ui/popover'
import { useFeatureFlag } from '@/features/flags'
import { cn } from '@/lib/utils'
import { useNotifications } from './api'
import { NotificationList } from './NotificationList'
import { useNotificationStream } from './stream'
import { NotificationToast } from './NotificationToast'
import type { NotificationView } from './types'

/** The bell in the navigation and the panel it opens; renders nothing while the `notifications` flag is off (ADR 0019). */
export function NotificationBell() {
  const enabled = useFeatureFlag('notifications')
  return enabled ? <BellWithPanel /> : null
}

function BellWithPanel() {
  const intl = useIntl()
  const notifications = useNotifications()
  const [toast, setToast] = useState<NotificationView>()
  useNotificationStream(setToast)
  const count = notifications.data?.pages.reduce((total, page) => total + page.items.length, 0) ?? 0
  const more = notifications.data?.pages.at(-1)?.nextCursor != null

  return (
    <>
      <Popover>
        <PopoverTrigger
          aria-label={intl.formatMessage({ id: 'notifications.bell.label' }, { count })}
          className={cn(shellTabClass, 'cursor-pointer lg:shrink-0 lg:px-2')}
        >
          <span className="relative pt-1 pr-2">
            <Bell className="size-5 shrink-0 lg:size-4" aria-hidden />
            {count > 0 && (
              <span
                aria-hidden
                className="absolute top-0 right-0 grid min-w-4 place-items-center rounded-full bg-primary px-1 text-[0.625rem] leading-4 font-semibold text-primary-foreground"
              >
                {more ? intl.formatMessage({ id: 'notifications.badge.more' }, { count }) : count}
              </span>
            )}
          </span>
          <span aria-hidden className="[overflow-wrap:anywhere] lg:hidden">
            {intl.formatMessage({ id: 'notifications.panel.title' })}
          </span>
        </PopoverTrigger>
        <PopoverContent>
          <PopoverTitle className="border-b p-4">
            {intl.formatMessage({ id: 'notifications.panel.title' })}
          </PopoverTitle>
          <NotificationList />
        </PopoverContent>
      </Popover>
      {toast && (
        <NotificationToast
          key={toast.id}
          notification={toast}
          onDone={() => {
            setToast(undefined)
          }}
        />
      )}
    </>
  )
}
