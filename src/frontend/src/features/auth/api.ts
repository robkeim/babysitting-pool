import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiFetch } from '@/lib/api/client'
import type { InvitationPreview, MemberView } from './types'

export const authKeys = {
  me: () => ['auth', 'me'] as const,
  invitation: (token: string) => ['auth', 'invitation', token] as const,
}

/** The signed-in Member; an error with status 401 means nobody is signed in. */
export function useMe() {
  return useQuery({
    queryKey: authKeys.me(),
    queryFn: () => apiFetch<MemberView>('/api/me'),
    retry: false,
    staleTime: 60_000,
  })
}

export function isUnauthenticated(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401
}

export function useRequestSignInLink() {
  return useMutation({
    mutationFn: (email: string) =>
      apiFetch<undefined>('/api/auth/sign-in-links', { method: 'POST', body: { email } }),
  })
}

/** Starts a session from a sign-in link; the next `useMe` then answers with the Member. */
export function useStartSession() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (token: string) =>
      apiFetch<undefined>('/api/auth/sessions', { method: 'POST', body: { token } }),
    onSuccess: () => client.invalidateQueries({ queryKey: authKeys.me() }),
  })
}

export function useInvitationPreview(token: string) {
  return useQuery({
    queryKey: authKeys.invitation(token),
    queryFn: () =>
      apiFetch<InvitationPreview>('/api/invitations/preview', { method: 'POST', body: { token } }),
    retry: false,
  })
}

export function useAcceptInvitation(token: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (displayName: string) =>
      apiFetch<undefined>('/api/invitations/accept', {
        method: 'POST',
        body: { token, displayName },
      }),
    onSuccess: () => client.invalidateQueries({ queryKey: authKeys.me() }),
  })
}

/** Saves the Member's Language and applies it at once; the server confirms on the next `me` fetch. */
export function useChangeLanguage() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (language: string) =>
      apiFetch<undefined>('/api/me/language', { method: 'PUT', body: { language } }),
    onSuccess: (_data, language) => {
      client.setQueryData<MemberView>(authKeys.me(), (me) => (me ? { ...me, language } : me))
    },
  })
}

/** Saves the Member's Color Scheme and applies it at once; the server confirms on the next `me` fetch. */
export function useChangeColorScheme() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (colorScheme: string) =>
      apiFetch<undefined>('/api/me/color-scheme', { method: 'PUT', body: { colorScheme } }),
    onSuccess: (_data, colorScheme) => {
      client.setQueryData<MemberView>(authKeys.me(), (me) => (me ? { ...me, colorScheme } : me))
    },
  })
}

export function useSignOut() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => apiFetch<undefined>('/api/auth/sessions/current', { method: 'DELETE' }),
    // Nothing cached for the Member may outlive their session. Reset rather than clear: clear()
    // detaches mounted observers (the shell's `useMe`) so they keep showing the old Member.
    onSuccess: () => client.resetQueries(),
  })
}
