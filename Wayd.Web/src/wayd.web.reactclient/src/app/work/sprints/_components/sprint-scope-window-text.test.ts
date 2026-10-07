import { SizingMethod, SprintScopeDto } from '@/src/services/wayd-api'
import { sprintScopeWindowText } from './sprint-scope-window-text'

// Started at noon in Chicago, ending at the end of its last planned day there.
const scope = {
  sizingMethod: SizingMethod.StoryPoints,
  effectiveStart: '2026-09-28T17:00:00Z',
  startIsActual: true,
  effectiveEnd: '2026-10-12T05:00:00Z',
  endIsActual: false,
  timeZone: 'America/Chicago',
  hasTeam: true,
} as unknown as SprintScopeDto

const during = new Date('2026-10-06T12:00:00Z')

describe('sprintScopeWindowText', () => {
  it('shows a recorded start on the viewer’s clock, as the sprint’s details do', () => {
    // Arrange / Act
    const result = sprintScopeWindowText(scope, 'America/Los_Angeles', during)

    // Assert
    expect(result[0]).toBe(
      'Committed at Sep 28, 2026, 10:00 AM PDT, when the team started the sprint.',
    )
  })

  it('shows a default end as the day it closes, not midnight of the next', () => {
    // Arrange / Act
    const result = sprintScopeWindowText(scope, 'America/Los_Angeles', during)

    // Assert
    expect(result[1]).toBe(
      'Ends at the end of Oct 11, 2026, its last planned day, or when the next sprint starts if sooner.',
    )
  })

  it('shows a default commitment point as the day it closes', () => {
    // Arrange / Act
    const result = sprintScopeWindowText(
      {
        ...scope,
        startIsActual: false,
        effectiveStart: '2026-09-29T05:00:00Z' as unknown as Date,
      },
      'America/Los_Angeles',
      during,
    )

    // Assert
    expect(result[0]).toBe(
      'Committed at the end of Sep 28, 2026, when the commitment grace period after the planned start ended.',
    )
  })

  it('speaks of the end in the past once it has passed', () => {
    // Arrange / Act
    const result = sprintScopeWindowText(
      {
        ...scope,
        endIsActual: true,
        effectiveEnd: '2026-10-09T20:30:00Z' as unknown as Date,
      },
      'America/Los_Angeles',
      new Date('2026-10-10T00:00:00Z'),
    )

    // Assert
    expect(result[1]).toBe(
      'Ended at Oct 9, 2026, 1:30 PM PDT, when the team completed the sprint.',
    )
  })

  it('says which zone days are counted in', () => {
    // Arrange / Act
    const result = sprintScopeWindowText(scope, 'America/Los_Angeles', during)

    // Assert
    expect(result[2]).toBe(
      'Days are counted in the team’s zone, America/Chicago.',
    )
  })

  it('labels a sprint with no team as using the system defaults', () => {
    // Arrange / Act
    const result = sprintScopeWindowText(
      { ...scope, hasTeam: false, timeZone: 'UTC' },
      'America/Los_Angeles',
      during,
    )

    // Assert
    expect(result[2]).toBe(
      'This sprint has no team, so it uses the system default zone (UTC) and grace period, and counts work items.',
    )
  })
})
