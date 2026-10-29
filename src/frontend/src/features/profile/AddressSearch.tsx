import { useEffect, useId, useState, type KeyboardEvent } from 'react'
import { FormattedMessage } from 'react-intl'
import { Input } from '@/components/ui/input'
import { cn } from 'cn'
import { MIN_QUERY_LENGTH, useAddressSuggestions } from './api'
import type { AddressSuggestion } from './types'

const DEBOUNCE_MS = 300

interface AddressSearchProps {
  id: string
  value: string
  /** False once the text is a picked suggestion (or the saved address), so nothing more is searched. */
  searching: boolean
  describedBy?: string
  onChange: (value: string) => void
  onPick: (suggestion: AddressSuggestion) => void
}

/**
 * A search box over the French national address base (ADR 0020). The text alone is never an address: the
 * Member must pick a suggestion. Follows the ARIA combobox pattern with a listbox popup.
 */
export function AddressSearch({
  id,
  value,
  searching,
  describedBy,
  onChange,
  onPick,
}: AddressSearchProps) {
  const listId = useId()
  const [debounced, setDebounced] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(-1)

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebounced(value.trim())
    }, DEBOUNCE_MS)
    return () => {
      clearTimeout(timer)
    }
  }, [value])

  const query = searching ? debounced : ''
  const suggestions = useAddressSuggestions(query)
  const items = suggestions.data?.items ?? []
  const showPopup = open && query.length >= MIN_QUERY_LENGTH

  function pick(suggestion: AddressSuggestion) {
    setOpen(false)
    setActive(-1)
    onPick(suggestion)
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Escape') {
      setOpen(false)
    } else if (event.key === 'ArrowDown' && items.length > 0) {
      event.preventDefault()
      setOpen(true)
      setActive((index) => (index + 1) % items.length)
    } else if (event.key === 'ArrowUp' && items.length > 0) {
      event.preventDefault()
      setActive((index) => (index <= 0 ? items.length : index) - 1)
    } else if (event.key === 'Enter' && showPopup && items[active]) {
      event.preventDefault()
      pick(items[active])
    }
  }

  return (
    <div className="relative">
      <Input
        id={id}
        className="h-11 lg:h-8"
        role="combobox"
        aria-expanded={showPopup && items.length > 0}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={active >= 0 ? `${listId}-${active}` : undefined}
        aria-describedby={describedBy}
        autoComplete="street-address"
        name="address"
        maxLength={200}
        value={value}
        onChange={(event) => {
          onChange(event.target.value)
          setOpen(true)
          setActive(-1)
        }}
        onKeyDown={onKeyDown}
        onFocus={() => {
          setOpen(true)
        }}
        onBlur={() => {
          setOpen(false)
        }}
      />
      <ul
        id={listId}
        role="listbox"
        className={cn(
          'absolute z-10 mt-1 max-h-64 w-full overflow-auto rounded-lg border bg-popover text-popover-foreground shadow-md',
          !(showPopup && items.length > 0) && 'hidden',
        )}
      >
        {items.map((suggestion, index) => (
          <li
            key={suggestion.banId}
            id={`${listId}-${index}`}
            role="option"
            aria-selected={index === active}
            className={cn(
              'cursor-pointer px-2.5 py-3 text-sm lg:py-1.5',
              index === active && 'bg-accent text-accent-foreground',
            )}
            // Picks on press, and keeps focus in the input so the blur above does not close the list first.
            onMouseDown={(event) => {
              event.preventDefault()
              pick(suggestion)
            }}
          >
            {suggestion.label}
          </li>
        ))}
      </ul>
      {showPopup && (
        <p aria-live="polite" className="mt-1 text-sm text-muted-foreground">
          {suggestions.isError ? (
            <FormattedMessage id="profile.search.unavailable" />
          ) : suggestions.isSuccess && items.length === 0 ? (
            <FormattedMessage id="profile.search.none" />
          ) : null}
        </p>
      )}
    </div>
  )
}
