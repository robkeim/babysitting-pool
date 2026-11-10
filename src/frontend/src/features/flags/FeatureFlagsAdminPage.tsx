import { useState, type SyntheticEvent } from 'react'
import { Flag } from 'lucide-react'
import { FormattedMessage, useIntl } from 'react-intl'
import { EmptyState } from '@/components/EmptyState'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { ApiError } from '@/lib/api/client'
import { type MessageId } from '@/lib/i18n'
import { useAdminFeatureFlags, useUpdateFeatureFlag } from './api'
import type { FeatureFlagView } from './types'
import { ErrorReference } from '@/components/ErrorReference'

const ROLES = ['Member', 'Admin'] as const

const ROLE_MESSAGES: Record<(typeof ROLES)[number], MessageId> = {
  Member: 'flags.admin.role.Member',
  Admin: 'flags.admin.role.Admin',
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function parseIds(text: string): string[] | undefined {
  const ids = text
    .split(/[\s,]+/)
    .map((id) => id.trim())
    .filter((id) => id !== '')
  return ids.every((id) => UUID.test(id)) ? ids : undefined
}

function parsePercent(text: string): number | undefined {
  return /^\d{1,3}$/.test(text) && Number(text) <= 100 ? Number(text) : undefined
}

function saveFailure(error: unknown): MessageId {
  return error instanceof ApiError && error.code === 'feature_flag_not_found'
    ? 'error.feature_flag_not_found'
    : 'common.somethingWentWrong'
}

function FlagCard({ flag }: { flag: FeatureFlagView }) {
  const intl = useIntl()
  const update = useUpdateFeatureFlag()
  const [enabled, setEnabled] = useState(flag.enabled)
  const [rollout, setRollout] = useState(String(flag.rolloutPercent))
  const [roleRollouts, setRoleRollouts] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      ROLES.map((role) => [
        role,
        String(flag.roleRollouts.find((r) => r.role === role)?.rolloutPercent ?? 0),
      ]),
    ),
  )
  const [allowed, setAllowed] = useState(flag.allowedMemberIds.join('\n'))
  const [denied, setDenied] = useState(flag.deniedMemberIds.join('\n'))

  const rolloutPercent = parsePercent(rollout)
  const roleValues = ROLES.map((role) => parsePercent(roleRollouts[role] ?? ''))
  const allowedIds = parseIds(allowed)
  const deniedIds = parseIds(denied)
  const valid =
    rolloutPercent !== undefined &&
    roleValues.every((v) => v !== undefined) &&
    allowedIds !== undefined &&
    deniedIds !== undefined

  function submit(event: SyntheticEvent) {
    event.preventDefault()
    if (!valid) return
    update.mutate({
      name: flag.name,
      enabled,
      rolloutPercent,
      allowedMemberIds: allowedIds,
      deniedMemberIds: deniedIds,
      roleRollouts: ROLES.flatMap((role, i) => {
        const percent = roleValues[i]
        return percent ? [{ role, rolloutPercent: percent }] : []
      }),
    })
  }

  const nameId = `flag-${flag.name}`

  return (
    <Card role="group" aria-labelledby={nameId}>
      <CardHeader>
        <CardTitle>
          <h3 id={nameId}>{flag.name}</h3>
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {flag.removalIssue == null ? (
            <FormattedMessage id="flags.admin.operatorToggle" />
          ) : (
            <FormattedMessage id="flags.admin.removal" values={{ issue: flag.removalIssue }} />
          )}
        </p>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-4">
          <div>
            <label className="flex min-h-11 items-center gap-3 text-sm font-medium lg:min-h-0 lg:gap-2">
              <input
                type="checkbox"
                className="size-5 accent-primary lg:size-4"
                checked={enabled}
                onChange={(event) => {
                  setEnabled(event.target.checked)
                }}
              />
              <FormattedMessage id="flags.admin.enabled" />
            </label>
            <p className="text-sm text-muted-foreground">
              <FormattedMessage id="flags.admin.enabled.hint" />
            </p>
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <label htmlFor={`${nameId}-rollout`} className="text-sm font-medium">
                <FormattedMessage id="flags.admin.rollout" />
              </label>
              <Input
                id={`${nameId}-rollout`}
                inputMode="numeric"
                value={rollout}
                aria-invalid={rolloutPercent === undefined}
                onChange={(event) => {
                  setRollout(event.target.value)
                }}
              />
              {rolloutPercent === undefined && (
                <p role="alert" className="text-sm text-destructive">
                  <FormattedMessage id="flags.admin.rollout.invalid" />
                </p>
              )}
            </div>
            {ROLES.map((role, i) => (
              <div key={role} className="space-y-2">
                <label htmlFor={`${nameId}-${role}`} className="text-sm font-medium">
                  <FormattedMessage
                    id="flags.admin.roleRollout"
                    values={{ role: intl.formatMessage({ id: ROLE_MESSAGES[role] }) }}
                  />
                </label>
                <Input
                  id={`${nameId}-${role}`}
                  inputMode="numeric"
                  value={roleRollouts[role]}
                  aria-invalid={roleValues[i] === undefined}
                  onChange={(event) => {
                    setRoleRollouts({ ...roleRollouts, [role]: event.target.value })
                  }}
                />
                {roleValues[i] === undefined && (
                  <p role="alert" className="text-sm text-destructive">
                    <FormattedMessage id="flags.admin.rollout.invalid" />
                  </p>
                )}
              </div>
            ))}
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <label htmlFor={`${nameId}-allowed`} className="text-sm font-medium">
                <FormattedMessage id="flags.admin.allowed" />
              </label>
              <textarea
                id={`${nameId}-allowed`}
                rows={3}
                className="w-full rounded-lg border border-input bg-transparent px-2.5 py-1.5 font-mono text-sm"
                value={allowed}
                aria-invalid={allowedIds === undefined}
                onChange={(event) => {
                  setAllowed(event.target.value)
                }}
              />
              {allowedIds === undefined && (
                <p role="alert" className="text-sm text-destructive">
                  <FormattedMessage id="flags.admin.ids.invalid" />
                </p>
              )}
            </div>
            <div className="space-y-2">
              <label htmlFor={`${nameId}-denied`} className="text-sm font-medium">
                <FormattedMessage id="flags.admin.denied" />
              </label>
              <textarea
                id={`${nameId}-denied`}
                rows={3}
                className="w-full rounded-lg border border-input bg-transparent px-2.5 py-1.5 font-mono text-sm"
                value={denied}
                aria-invalid={deniedIds === undefined}
                onChange={(event) => {
                  setDenied(event.target.value)
                }}
              />
              {deniedIds === undefined && (
                <p role="alert" className="text-sm text-destructive">
                  <FormattedMessage id="flags.admin.ids.invalid" />
                </p>
              )}
            </div>
          </div>
          {update.isError && (
            <Alert variant="destructive">
              <AlertTitle>
                <FormattedMessage id="flags.admin.saveFailed.title" values={{ name: flag.name }} />
              </AlertTitle>
              <AlertDescription>
                <FormattedMessage id={saveFailure(update.error)} />
                <ErrorReference error={update.error} />
              </AlertDescription>
            </Alert>
          )}
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center">
            <Button
              type="submit"
              className="h-11 w-full lg:h-8 lg:w-auto"
              disabled={update.isPending || !valid}
              aria-label={intl.formatMessage({ id: 'flags.admin.saveAria' }, { name: flag.name })}
            >
              <FormattedMessage id="flags.admin.save" />
            </Button>
            {update.isSuccess && (
              <p role="status" className="text-sm">
                <FormattedMessage id="flags.admin.saved" />
              </p>
            )}
          </div>
        </form>
      </CardContent>
    </Card>
  )
}

/** The Admin's view of every feature flag: its master switch, rollout and per-member lists (ADR 0016). */
export function FeatureFlagsAdminPage() {
  const flags = useAdminFeatureFlags()

  if (flags.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>
          <FormattedMessage id="flags.admin.loadFailed.title" />
        </AlertTitle>
        <AlertDescription>
          <FormattedMessage id="flags.admin.loadFailed.body" />
          <ErrorReference error={flags.error} />
        </AlertDescription>
      </Alert>
    )
  }
  if (!flags.data) return null
  if (flags.data.items.length === 0) {
    return (
      <EmptyState icon={Flag}>
        <FormattedMessage id="flags.admin.empty" />
      </EmptyState>
    )
  }

  return (
    <div className="space-y-4">
      {flags.data.items.map((flag) => (
        <FlagCard key={flag.name} flag={flag} />
      ))}
    </div>
  )
}
