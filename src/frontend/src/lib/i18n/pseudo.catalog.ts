import { enUS } from './messages'
import { pseudoLocalize } from './pseudo'

// Its own chunk: production downloads it only when the Member picks Ünïcödé Tëst (ADR 0009).
export default pseudoLocalize(enUS)
