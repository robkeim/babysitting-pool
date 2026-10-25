import type { components } from '@/lib/api/schema.gen'

export type AccountView = components['schemas']['AccountView']
export type LedgerEntryView = components['schemas']['LedgerEntryView']
export type LedgerEntryKind = LedgerEntryView['kind']
