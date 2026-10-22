import type { components } from '@/lib/api/schema.gen'

export type RequestView = components['schemas']['RequestView']
export type RequestStatus = components['schemas']['RequestStatus']
export type BabysittingLocation = components['schemas']['BabysittingLocation']
export type TransportOption = components['schemas']['TransportOption']
export type PostRequestBody = components['schemas']['PostRequestBody']
export type CostQuote = components['schemas']['CostQuote']
export type RequestPage = components['schemas']['PageOfRequestView']

/** The cost drivers the live preview sends (spec §3). */
export type QuoteDetails = Omit<PostRequestBody, 'startsAt' | 'area' | 'address'>
