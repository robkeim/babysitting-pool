import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, vi } from 'vitest'
import { FeatureFlagsTestProvider } from '@/features/flags'
import { FakeEventSource } from '@/test/eventSource'
import { renderWithProviders } from '@/test/render'
import { server } from '@/test/server'
import { NotificationBell } from './NotificationBell'
import {
  claimedNotification,
  claimedRequestsHandler,
  notificationsHandler,
  openRequestIdsHandler,
  postedNotification,
} from './mocks'

function stream() {
  const source = FakeEventSource.instances.at(-1)
  if (!source) throw new Error('the stream was not opened')
  return source
}

function renderBell(enabled = true) {
  return renderWithProviders(
    <FeatureFlagsTestProvider flags={{ notifications: enabled }}>
      <NotificationBell />
    </FeatureFlagsTestProvider>,
  )
}

beforeEach(() => {
  server.use(openRequestIdsHandler([postedNotification.requestId]), claimedRequestsHandler([]))
})
afterEach(() => {
  vi.useRealTimers()
})

describe('the live Notification stream', () => {
  it('opens on the stream path while the flag is on, and closes when the bell goes', async () => {
    server.use(notificationsHandler([]))
    const { unmount } = renderBell()

    await screen.findByRole('button', { name: /^Notifications/ })
    expect(stream().url).toBe('/api/notifications/stream')

    const source = stream()
    unmount()
    expect(source.closed).toBe(true)
  })

  it('stays closed while the flag is off', () => {
    renderBell(false)

    expect(FakeEventSource.instances).toHaveLength(0)
  })

  it('adds a created Notification to the badge and list and announces it in a toast', async () => {
    server.use(notificationsHandler([claimedNotification]))
    renderBell()
    await screen.findByRole('button', { name: 'Notifications, 1 waiting' })

    stream().emit('notification.created', postedNotification)

    expect(await screen.findByRole('button', { name: 'Notifications, 2 waiting' })).toBeVisible()
    server.use(notificationsHandler([postedNotification, claimedNotification]))
    expect(screen.getByRole('status')).toHaveTextContent('New request in Riverside on')
    await userEvent.click(screen.getByRole('button', { name: /^Notifications/ }))
    const rows = await within(await screen.findByRole('dialog')).findAllByRole('listitem')
    expect(rows[0]).toHaveTextContent('New request in Riverside')
  })

  it('does not count a Notification twice when it arrives again', async () => {
    server.use(notificationsHandler([postedNotification]))
    renderBell()
    await screen.findByRole('button', { name: 'Notifications, 1 waiting' })

    stream().emit('notification.created', postedNotification)

    expect(screen.getByRole('button', { name: 'Notifications, 1 waiting' })).toBeVisible()
  })

  it('lets the toast go after a few seconds', async () => {
    server.use(notificationsHandler([]))
    renderBell()
    await screen.findByRole('button', { name: /^Notifications/ })
    vi.useFakeTimers({ shouldAdvanceTime: true })

    stream().emit('notification.created', claimedNotification)
    expect(screen.getByRole('status')).toHaveTextContent('A sitter claimed your request.')
    act(() => {
      vi.advanceTimersByTime(6000)
    })

    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('removes a Notification dismissed elsewhere', async () => {
    server.use(notificationsHandler([claimedNotification, postedNotification]))
    renderBell()
    await screen.findByRole('button', { name: 'Notifications, 2 waiting' })

    stream().emit('notification.dismissed', { id: postedNotification.id })

    expect(await screen.findByRole('button', { name: 'Notifications, 1 waiting' })).toBeVisible()
  })

  it('empties the list when all are dismissed elsewhere', async () => {
    server.use(notificationsHandler([claimedNotification, postedNotification]))
    renderBell()
    await screen.findByRole('button', { name: 'Notifications, 2 waiting' })

    stream().emit('notifications.cleared', {})

    expect(await screen.findByRole('button', { name: 'Notifications' })).toBeVisible()
  })

  it('replaces the list from the server after a reconnect, not on the first connect', async () => {
    server.use(notificationsHandler([claimedNotification]))
    renderBell()
    await screen.findByRole('button', { name: 'Notifications, 1 waiting' })
    stream().emit('open')
    server.use(notificationsHandler([claimedNotification, postedNotification]))
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(screen.getByRole('button', { name: 'Notifications, 1 waiting' })).toBeVisible()

    stream().emit('error')
    stream().emit('open')

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Notifications, 2 waiting' })).toBeVisible()
    })
  })
})
