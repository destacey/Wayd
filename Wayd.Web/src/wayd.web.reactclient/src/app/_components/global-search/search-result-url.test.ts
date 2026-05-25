import { GlobalSearchResultItemDto } from '@/src/services/wayd-api'
import { getSearchResultUrl } from './search-result-url'

const item = (
  entityType: string,
  key: string,
  auxKey?: string,
): GlobalSearchResultItemDto =>
  ({ entityType, key, auxKey } as GlobalSearchResultItemDto)

describe('getSearchResultUrl', () => {
  describe('Work', () => {
    it('returns work item url', () => {
      expect(getSearchResultUrl(item('WorkItem', 'WAYD-123', 'ws-1'))).toBe(
        '/work/workspaces/ws-1/work-items/WAYD-123',
      )
    })

    it('returns workspace url', () => {
      expect(getSearchResultUrl(item('Workspace', 'ws-1'))).toBe(
        '/work/workspaces/ws-1',
      )
    })
  })

  describe('Organization', () => {
    it('returns team url using auxKey', () => {
      expect(getSearchResultUrl(item('Team', 'TT1', 'abc-123'))).toBe(
        '/organizations/teams/abc-123',
      )
    })

    it('returns team of teams url using auxKey', () => {
      expect(getSearchResultUrl(item('TeamOfTeams', 'TOT1', 'def-456'))).toBe(
        '/organizations/team-of-teams/def-456',
      )
    })

    it('returns employee url', () => {
      expect(getSearchResultUrl(item('Employee', 'EMP-1'))).toBe(
        '/organizations/employees/EMP-1',
      )
    })
  })

  describe('Planning', () => {
    it('returns sprint url', () => {
      expect(getSearchResultUrl(item('Iteration', '42'))).toBe(
        '/planning/sprints/42',
      )
    })

    it('returns planning interval url', () => {
      expect(getSearchResultUrl(item('PlanningInterval', '7'))).toBe(
        '/planning/planning-intervals/7',
      )
    })

    it('returns PI iteration url', () => {
      expect(
        getSearchResultUrl(item('PlanningIntervalIteration', '5', '3')),
      ).toBe('/planning/planning-intervals/3/iterations/5')
    })

    it('returns PI objective url', () => {
      expect(
        getSearchResultUrl(item('PlanningIntervalObjective', '10', '3')),
      ).toBe('/planning/planning-intervals/3/objectives/10')
    })

    it('returns PI team plan review url with team code hash', () => {
      expect(getSearchResultUrl(item('PiTeam', 'JUICE', '3|juice'))).toBe(
        '/planning/planning-intervals/3/plan-review#juice',
      )
    })

    it('returns PI team url with missing auxKey gracefully', () => {
      expect(getSearchResultUrl(item('PiTeam', 'JUICE', undefined))).toBe(
        '/planning/planning-intervals',
      )
    })

    it('returns roadmap url', () => {
      expect(getSearchResultUrl(item('Roadmap', '42'))).toBe(
        '/planning/roadmaps/42',
      )
    })
  })

  describe('PPM', () => {
    it('returns project url', () => {
      expect(getSearchResultUrl(item('Project', 'PROJ'))).toBe(
        '/ppm/projects/PROJ',
      )
    })

    it('returns program url', () => {
      expect(getSearchResultUrl(item('Program', 'PROG'))).toBe(
        '/ppm/programs/PROG',
      )
    })

    it('returns portfolio url', () => {
      expect(getSearchResultUrl(item('ProjectPortfolio', 'PORT'))).toBe(
        '/ppm/portfolios/PORT',
      )
    })

    it('returns strategic initiative url', () => {
      expect(getSearchResultUrl(item('StrategicInitiative', 'SI-1'))).toBe(
        '/ppm/strategic-initiatives/SI-1',
      )
    })
  })

  describe('Unknown', () => {
    it('returns / for unknown entity type', () => {
      expect(getSearchResultUrl(item('Unknown', 'X'))).toBe('/')
    })
  })
})
