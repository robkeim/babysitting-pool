import { readFileSync } from 'node:fs'
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/server'
import { renderWithProviders } from '@/test/render'
import { enUS } from '@/lib/i18n/messages'
import { CreditsPage } from './CreditsPage'
import { accountFailureHandler, accountHandler, accountPagesHandler } from './mocks'

// Vitest runs from src/frontend, next to src/backend. The OpenAPI schema types `kind` as a plain string.
const ledgerEntryKinds = [
  ...readFileSync('../backend/BabysittingPool.Domain/Credits/LedgerEntryKind.cs', 'utf8').matchAll(
    /^\s+(\w+),?\s*$/gm,
  ),
].map((match) => match[1] ?? '')

describe('CreditsPage', () => {
  it('labels every backend Ledger entry kind', async () => {
    expect(ledgerEntryKinds.length).toBeGreaterThan(0)
    server.use(
      accountHandler({
        availableQuarterCredits: 0,
        escrowedQuarterCredits: 0,
        ledger: {
          items: ledgerEntryKinds.map((kind, index) => ({
            id: `2f6c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e${String(index).padStart(2, '0')}`,
            kind,
            amountQuarterCredits: 1,
            occurredAt: '2026-03-01T09:00:00Z',
          })),
          nextCursor: null,
        },
      }),
    )

    renderWithProviders(<CreditsPage />)

    await screen.findByRole('table')
    const cells = screen
      .getAllByRole('row')
      .slice(1)
      .map((row) => within(row).getAllByRole('cell')[1])
    for (const [index, kind] of ledgerEntryKinds.entries()) {
      expect(enUS, kind).toHaveProperty(`credits.ledger.kind.${kind}`)
      expect(cells[index]).toHaveTextContent(
        enUS[`credits.ledger.kind.${kind}` as keyof typeof enUS],
      )
    }
  })

  it('shows the balance and Ledger entries as credits', async () => {
    server.use(
      accountHandler({
        availableQuarterCredits: 41,
        escrowedQuarterCredits: 8,
        ledger: {
          items: [
            {
              id: '2f6c1c3e-5d8a-4a38-9b1d-0a7a1c1b9e11',
              kind: 'Grant',
              amountQuarterCredits: 41,
              occurredAt: '2026-03-01T09:00:00Z',
            },
          ],
          nextCursor: null,
        },
      }),
    )

    renderWithProviders(<CreditsPage />)

    const available = await screen.findByRole('group', { name: 'Available' })
    expect(within(available).getByText('10.25')).toBeInTheDocument()
    expect(
      within(screen.getByRole('group', { name: 'In escrow' })).getByText('2.00'),
    ).toBeInTheDocument()
    const row = screen.getByRole('row', { name: /grant/i })
    expect(within(row).getByText('10.25')).toBeInTheDocument()
    expect(within(row).getByText(/Mar 1, 2026/)).toBeInTheDocument()
  })

  it('shows an error without exposing the server message when the account cannot be loaded', async () => {
    server.use(accountFailureHandler())

    renderWithProviders(<CreditsPage />)

    expect(await screen.findByRole('alert')).toHaveTextContent(/couldn.t load your credits/i)
    expect(screen.queryByText('Boom')).not.toBeInTheDocument()
  })

  it('loads the next page of Ledger entries on request', async () => {
    const entry = (id: string, amountQuarterCredits: number) => ({
      id,
      kind: 'Grant',
      amountQuarterCredits,
      occurredAt: '2026-03-01T09:00:00Z',
    })
    server.use(
      accountPagesHandler({
        '': {
          availableQuarterCredits: 5,
          escrowedQuarterCredits: 0,
          ledger: { items: [entry('a', 1)], nextCursor: 'next-page' },
        },
        'next-page': {
          availableQuarterCredits: 5,
          escrowedQuarterCredits: 0,
          ledger: { items: [entry('b', 4)], nextCursor: null },
        },
      }),
    )
    renderWithProviders(<CreditsPage />)

    await userEvent.click(await screen.findByRole('button', { name: /load more/i }))

    expect(await screen.findByRole('row', { name: /1\.00/ })).toBeInTheDocument()
    expect(screen.getByRole('row', { name: /0\.25/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /load more/i })).not.toBeInTheDocument()
  })
})
