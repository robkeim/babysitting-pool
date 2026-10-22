import enUS from './en-US.json'

export type MessageId = keyof typeof enUS

declare global {
  // eslint-disable-next-line @typescript-eslint/no-namespace -- react-intl's documented hook for typed message ids
  namespace FormatjsIntl {
    interface Message {
      ids: MessageId
    }
  }
}

export { enUS }
