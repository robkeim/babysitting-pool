import { act } from '@testing-library/react'

/** Stands in for the browser's `EventSource` (jsdom has none); tests push events through `emit`. */
export class FakeEventSource {
  static instances: FakeEventSource[] = []
  private listeners = new Map<string, ((event: Event) => void)[]>()
  closed = false

  readonly url: string

  constructor(url: string) {
    this.url = url
    FakeEventSource.instances.push(this)
  }

  addEventListener(type: string, listener: (event: Event) => void) {
    this.listeners.set(type, [...(this.listeners.get(type) ?? []), listener])
  }

  close() {
    this.closed = true
  }

  emit(type: string, data?: unknown) {
    const event = new MessageEvent(type, data === undefined ? {} : { data: JSON.stringify(data) })
    act(() => {
      this.listeners.get(type)?.forEach((listener) => {
        listener(event)
      })
    })
  }
}
