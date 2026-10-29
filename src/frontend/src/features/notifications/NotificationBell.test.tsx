import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { renderWithProviders } from '@/test/render'
import { server } from '@/test/server'
import { NotificationBell } from './NotificationBell'
import {
  claimedNotification,
  claimedRequestsHandler,
  dismissAllNotificationsHandler,
  dismissNotificationHandler,
  notificationsFailureHandler,
  notificationsHandler,
  openRequestIdsHandler,
  postedNotification,
} from './mocks'

function renderBell() {
  return renderWithProviders(<NotificationBell />)
}

const claimedRequest = {
  id: claimedNotification.requestId,
  startsAt: '2026-03-02T10:00:00Z',
  sitterDisplayName: 'Grace',
}

function listsHandlers(open = [postedNotification.requestId]) {
  return [openRequestIdsHandler(open), claimedRequestsHandler([claimedRequest])]
}

async function openPanel() {
  await userEvent.click(await screen.findByRole('button', { name: /^Notifications/ }))
  return screen.findByRole('dialog')
}

describe('NotificationBell', () => {
  it('shows that the Notifications are loading until they arrive', async () => {
    server.use(
      http.get('/api/notifications', async () => {
        await delay(300)
        return HttpResponse.json({ items: [], nextCursor: null })
      }),
      ...listsHandlers(),
    )
    renderBell()

    const panel = await openPanel()

    expect(within(panel).getByRole('status')).toHaveTextContent('Loading your notifications…')
    expect(await within(panel).findByText("You're all caught up.")).toBeInTheDocument()
    expect(within(panel).queryByRole('status')).not.toBeInTheDocument()
  })

  it('lists the Notifications in the order given, with their count on the bell', async () => {
    server.use(notificationsHandler([claimedNotification, postedNotification]), ...listsHandlers())
    renderBell()

    expect(
      await screen.findByRole('button', { name: 'Notifications, 2 waiting' }),
    ).toBeInTheDocument()
    const panel = await openPanel()

    const rows = await within(panel).findAllByRole('listitem')
    expect(rows).toHaveLength(2)
    expect(rows[0]).toHaveTextContent('Grace claimed your request for Mar 2, 2026, 10:00 AM UTC')
    expect(rows[1]).toHaveTextContent('New request in Riverside on Mar 2, 2026, 10:00 AM UTC')
    expect(within(panel).getByRole('link', { name: /^Grace claimed/ })).toHaveAttribute(
      'href',
      '/requests',
    )
    expect(within(panel).getByRole('link', { name: /^New request/ })).toHaveAttribute(
      'href',
      '/open-requests',
    )
  })

  it('says so when there is nothing to show', async () => {
    server.use(notificationsHandler([]), ...listsHandlers())
    renderBell()

    const panel = await openPanel()

    expect(await within(panel).findByText("You're all caught up.")).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Notifications' })).toBeInTheDocument()
  })

  it('dismisses nothing when the panel opens or a row is clicked', async () => {
    let dismissed = 0
    server.use(
      notificationsHandler([postedNotification]),
      dismissNotificationHandler(() => (dismissed += 1)),
      dismissAllNotificationsHandler(() => (dismissed += 1)),
      ...listsHandlers(),
    )
    renderBell()

    const panel = await openPanel()
    const row = await within(panel).findByRole('listitem')
    row.addEventListener('click', (event) => {
      event.preventDefault()
    })
    await userEvent.click(within(row).getByRole('link'))

    expect(dismissed).toBe(0)
    expect(within(panel).getAllByRole('listitem')).toHaveLength(1)
  })

  it('dismisses one Notification and shows the list without it', async () => {
    let remaining = [claimedNotification, postedNotification]
    const dismissedIds: string[] = []
    server.use(
      notificationsHandler(remaining),
      dismissNotificationHandler((id) => {
        dismissedIds.push(id)
        remaining = remaining.filter((notification) => notification.id !== id)
        server.use(notificationsHandler(remaining))
      }),
      ...listsHandlers(),
    )
    renderBell()

    const panel = await openPanel()
    await userEvent.click(
      await within(panel).findByRole('button', { name: /^Dismiss: New request in Riverside/ }),
    )

    await waitFor(() => {
      expect(within(panel).getAllByRole('listitem')).toHaveLength(1)
    })
    expect(dismissedIds).toEqual([postedNotification.id])
    expect(screen.getByRole('button', { name: 'Notifications, 1 waiting' })).toBeInTheDocument()
  })

  it('dismisses all of them', async () => {
    let dismissedAll = 0
    server.use(
      notificationsHandler([claimedNotification, postedNotification]),
      dismissAllNotificationsHandler(() => {
        dismissedAll += 1
        server.use(notificationsHandler([]))
      }),
      ...listsHandlers(),
    )
    renderBell()

    const panel = await openPanel()
    await userEvent.click(await within(panel).findByRole('button', { name: 'Dismiss all' }))

    expect(await within(panel).findByText("You're all caught up.")).toBeInTheDocument()
    expect(dismissedAll).toBe(1)
  })

  it('shows "No longer available" for a Posted Request that is no longer open', async () => {
    server.use(notificationsHandler([postedNotification]), ...listsHandlers([]))
    renderBell()

    const panel = await openPanel()

    const row = await within(panel).findByRole('listitem')
    await waitFor(() => {
      expect(within(row).getByText('No longer available')).toBeInTheDocument()
    })
    expect(within(row).queryByRole('link')).not.toBeInTheDocument()
    expect(within(row).getByRole('button', { name: /^Dismiss:/ })).toBeInTheDocument()
  })

  it('does not call a Posted Request gone while the open list has more pages to load', async () => {
    server.use(
      notificationsHandler([postedNotification]),
      openRequestIdsHandler([], 'more'),
      claimedRequestsHandler([]),
    )
    renderBell()

    const panel = await openPanel()

    expect(await within(panel).findByRole('link')).toBeInTheDocument()
    expect(within(panel).queryByText('No longer available')).not.toBeInTheDocument()
  })

  it('falls back to generic copy when the claimed Request is not among My Requests', async () => {
    server.use(
      notificationsHandler([claimedNotification]),
      openRequestIdsHandler([]),
      claimedRequestsHandler([]),
    )
    renderBell()

    const panel = await openPanel()

    expect(await within(panel).findByText('A sitter claimed your request.')).toBeInTheDocument()
  })

  it('says it could not load the Notifications', async () => {
    server.use(notificationsFailureHandler(), ...listsHandlers())
    renderBell()

    const panel = await openPanel()

    expect(await within(panel).findByRole('alert')).toHaveTextContent(
      "We couldn't load your notifications.",
    )
  })
})
