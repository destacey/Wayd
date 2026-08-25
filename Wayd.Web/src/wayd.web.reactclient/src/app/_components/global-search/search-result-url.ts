import { GlobalSearchResultItemDto } from '@/src/services/wayd-api'

export function getSearchResultUrl(item: GlobalSearchResultItemDto): string {
  const { entityType, key, auxKey } = item

  switch (entityType) {
    // Work
    case 'WorkItem':
      return `/work/workspaces/${auxKey}/work-items/${key}`
    case 'Workspace':
      return `/work/workspaces/${key}`

    // Organization
    case 'Team':
      return `/organizations/teams/${auxKey}`
    case 'TeamOfTeams':
      return `/organizations/team-of-teams/${auxKey}`
    case 'Employee':
      return `/organizations/employees/${key}`

    // Planning
    case 'Iteration':
      return `/planning/sprints/${key}`
    case 'PlanningInterval':
      return `/planning/planning-intervals/${key}`
    case 'PlanningIntervalIteration':
      return `/planning/planning-intervals/${auxKey}/iterations/${key}`
    case 'PlanningIntervalObjective':
      return `/planning/planning-intervals/${auxKey}/objectives/${key}`
    case 'PiTeam': {
      const parts = auxKey?.split('|')
      if (!parts || parts.length !== 2 || !parts[0] || !parts[1])
        return '/planning/planning-intervals'
      return `/planning/planning-intervals/${parts[0]}/plan-review?team=${parts[1]}`
    }

    // Planning - Roadmaps
    case 'Roadmap':
      return `/planning/roadmaps/${key}`

    // PPM
    case 'Project':
      return `/ppm/projects/${key}`
    case 'Program':
      return `/ppm/programs/${key}`
    case 'ProjectPortfolio':
      return `/ppm/portfolios/${key}`
    case 'StrategicInitiative':
      return `/ppm/strategic-initiatives/${key}`

    // Docs
    case 'Doc':
      return key

    default:
      return '/'
  }
}
