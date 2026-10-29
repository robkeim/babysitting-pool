import type { components } from '@/lib/api/schema.gen'

export type ProfileView = components['schemas']['ProfileView']
export type AddressSuggestion = components['schemas']['AddressSuggestion']

/** What a save sends: the picked suggestion, or only the instructions to keep the saved address (ADR 0020). */
export interface ProfileUpdate {
  banId?: string
  label?: string
  instructions: string
}
