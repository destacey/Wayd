import {
  BacklogHealthCheckDto,
  TeamBacklogHealthDto,
} from '@/src/services/wayd-api'
import {
  BacklogHealthCheck,
  BacklogHealthOutcome,
  describeCheck,
  formatCheckValue,
  isMeasureCheck,
} from './backlog-health-formatting'

const check = (
  id: BacklogHealthCheck,
  overrides: Partial<BacklogHealthCheckDto> = {},
): BacklogHealthCheckDto => ({
  check: { id, name: BacklogHealthCheck[id] },
  outcome: { id: BacklogHealthOutcome.Assessed, name: 'Assessed' },
  ...overrides,
})

describe('formatCheckValue', () => {
  it('shows runway in weeks', () => {
    expect(
      formatCheckValue(check(BacklogHealthCheck.Runway, { value: 3.26 })),
    ).toBe('3.3 weeks')
  })

  it('shows net flow as a ratio', () => {
    expect(
      formatCheckValue(check(BacklogHealthCheck.NetFlow, { value: 1.5 })),
    ).toBe('1.5 created per completed')
  })

  it('says when net flow has nothing completed to compare with', () => {
    // The API sends the missing ratio as null.
    const noRatio = check(BacklogHealthCheck.NetFlow, {
      value: null as unknown as undefined,
    })

    expect(formatCheckValue(noRatio)).toBe('Nothing completed')
  })

  it('shows WIP load per member', () => {
    expect(
      formatCheckValue(check(BacklogHealthCheck.WipLoad, { value: 2 })),
    ).toBe('2 per member')
  })

  it('shows how many work items a check flagged', () => {
    expect(
      formatCheckValue(
        check(BacklogHealthCheck.Stale, { flagged: 3, inScope: 12, value: 25 }),
      ),
    ).toBe('3 of 12 (25%)')
  })

  it.each([
    [BacklogHealthOutcome.NotEnoughHistory, 'Not enough history'],
    [BacklogHealthOutcome.NotApplicable, 'Not applicable'],
  ])('explains a check that was not graded (%s)', (outcome, expected) => {
    expect(
      formatCheckValue(
        check(BacklogHealthCheck.AgingWip, {
          outcome: { id: outcome, name: '' },
        }),
      ),
    ).toBe(expected)
  })
})

describe('isMeasureCheck', () => {
  it('separates backlog measures from work item checks', () => {
    expect(isMeasureCheck(check(BacklogHealthCheck.Runway))).toBe(true)
    expect(isMeasureCheck(check(BacklogHealthCheck.RankInversion))).toBe(false)
  })
})

describe('describeCheck', () => {
  const health = {
    lookbackDays: 60,
    readinessWindowWorkItems: 8,
    memberCount: 4,
    minimumItemsCompleted: 10,
    agingWipDays: 12.34,
    oversizedStoryPoints: null,
    thresholds: {
      staleDays: 45,
      atRiskPercent: 10,
      unhealthyPercent: 25,
      agingWipPercentile: 85,
      oversizedPercentile: 90,
      runwayUnhealthyWeeks: 2,
      runwayAtRiskWeeks: 4,
      runwayTooLongWeeks: 26,
    },
  } as unknown as TeamBacklogHealthDto

  it('states the threshold a work item check flags against and how it is graded', () => {
    expect(describeCheck(BacklogHealthCheck.Stale, health)).toBe(
      'Work items not modified for 45 days or more. At Risk at 10% flagged, Unhealthy at 25%.',
    )
  })

  it('describes every check when the API sent its limits as null', () => {
    const noHistory = {
      ...health,
      agingWipDays: null,
      oversizedStoryPoints: null,
      memberCount: null,
    } as unknown as TeamBacklogHealthDto

    for (const id of Object.values(BacklogHealthCheck).filter(
      (v): v is BacklogHealthCheck => typeof v === 'number',
    )) {
      expect(describeCheck(id, noHistory)).not.toContain('null')
    }
    expect(describeCheck(BacklogHealthCheck.AgingWip, noHistory)).toContain(
      'Needs at least 10 completed work items',
    )
  })

  it('states the cycle time an aging work item is past', () => {
    expect(describeCheck(BacklogHealthCheck.AgingWip, health)).toContain(
      "open longer than 12.3 days, the team's 85th percentile cycle time",
    )
  })

  it('says a history-based check needs history when there is none', () => {
    expect(describeCheck(BacklogHealthCheck.Oversized, health)).toContain(
      "above the team's 90th percentile. Needs at least 10 completed work items",
    )
  })

  it('states the readiness window size', () => {
    expect(describeCheck(BacklogHealthCheck.NoParent, health)).toContain(
      'the top 8 ranked work items without a parent',
    )
  })

  it('states the runway limits and the history it is measured over', () => {
    expect(describeCheck(BacklogHealthCheck.Runway, health)).toBe(
      "Weeks the backlog lasts at the team's throughput over the last 60 days. Unhealthy below 2 weeks; At Risk below 4 or above 26 weeks.",
    )
  })
})
