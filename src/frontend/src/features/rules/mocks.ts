import { http, HttpResponse } from 'msw'
import type { RulesView } from './types'

export const sampleRules: RulesView = {
  startingBalanceQuarterCredits: 40,
  durationIncrementMinutes: 15,
  minimumLeadTimeMinutes: 360,
  claimCutoffMinutes: 180,
  demurrageDay: 'Monday',
  demurrageTime: '00:00',
  timeZone: 'UTC',
  demurrageGraceDays: 21,
  demurrageChargeQuarterCredits: 8,
  dormancyDays: 42,
  prices: {
    familyHouseQuarterCredits: 2,
    oneWayTransportQuarterCredits: 2,
    roundTripTransportQuarterCredits: 3,
    lunchQuarterCredits: 2,
    snackQuarterCredits: 1,
    dinnerQuarterCredits: 2,
    lunchPerChildQuarterCredits: 2,
    snackPerChildQuarterCredits: 1,
    dinnerPerChildQuarterCredits: 2,
    secondChildHourlyQuarterCredits: 2,
    additionalChildHourlyQuarterCredits: 1,
  },
  examples: [
    {
      durationMinutes: 90,
      childCount: 2,
      location: 'RequestingFamilysHouse',
      transport: 'None',
      lunch: true,
      snack: false,
      dinner: false,
      quote: {
        costQuarterCredits: 15,
        lines: {
          hourlyRateQuarterCredits: 6,
          timeQuarterCredits: 9,
          locationQuarterCredits: 2,
          transportQuarterCredits: 0,
          lunchQuarterCredits: 4,
          snackQuarterCredits: 0,
          dinnerQuarterCredits: 0,
        },
        options: {
          familyHouseQuarterCredits: 2,
          oneWayTransportQuarterCredits: 2,
          roundTripTransportQuarterCredits: 3,
          lunchQuarterCredits: 4,
          snackQuarterCredits: 2,
          dinnerQuarterCredits: 4,
          lunchPerChildQuarterCredits: 2,
          snackPerChildQuarterCredits: 1,
          dinnerPerChildQuarterCredits: 2,
          secondChildHourlyQuarterCredits: 2,
          additionalChildHourlyQuarterCredits: 1,
        },
      },
    },
  ],
}

export function rulesHandler(rules: RulesView = sampleRules) {
  return http.get('*/api/rules', () => HttpResponse.json(rules))
}

export function rulesFailureHandler() {
  return http.get('*/api/rules', () =>
    HttpResponse.json(
      { status: 500, title: 'Boom', code: 'internal_error' },
      { status: 500, headers: { 'content-type': 'application/problem+json' } },
    ),
  )
}
