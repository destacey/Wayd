import { SizingMethod, SprintScopeDto } from '@/src/services/wayd-api'
import { sprintScopeWindowText } from './sprint-scope-window-text'

const scope = {
  sizingMethod: SizingMethod.StoryPoints,
  effectiveStart: '2026-09-15T05:00:00Z',
  startIsActual: false,
  effectiveEnd: '2026-09-25T20:00:00Z',
  endIsActual: true,
  timeZone: 'America/Chicago',
  hasTeam: true,
} as unknown as SprintScopeDto

describe('sprintScopeWindowText', () => {
  it('says where each end came from, in the team’s zone', () => {
    // Arrange / Act
    const result = sprintScopeWindowText(scope)

    // Assert
    expect(result).toEqual([
      'Committed at Sep 15, 2026, 12:00 AM CDT, the end of the commitment grace period after the planned start.',
      'Ends at Sep 25, 2026, 3:00 PM CDT, when the team completed the sprint.',
      'Times are in the team’s zone, America/Chicago.',
    ])
  })

  it('labels a sprint with no team as using the system defaults', () => {
    // Arrange / Act
    const result = sprintScopeWindowText({
      ...scope,
      hasTeam: false,
      timeZone: 'UTC',
    })

    // Assert
    expect(result[2]).toBe(
      'This sprint has no team, so it uses the system default zone (UTC) and grace period, and counts work items.',
    )
  })
})
