import { BreadcrumbItem } from './types'

const routes = {
  organizations: {
    title: 'Organizations',
    href: null,
  },
  'functional-org-chart': {
    title: 'Functional Org Chart',
  },
  'team-of-teams': {
    title: 'Teams',
    href: '/organizations/teams',
  },
  employees: {
    title: 'Employees',
  },
  teams: {
    title: 'Teams',
  },

  planning: {
    title: 'Planning',
    href: null,
  },
  'planning-intervals': {
    title: 'Planning Intervals',
  },
  risks: {
    title: 'Teams',
    href: null,
  },
  roadmaps: {
    title: 'Roadmaps',
  },
  'poker-sessions': {
    title: 'Planning Poker',
    href: '/planning/poker-sessions',
  },
  sprints: {
    title: 'Sprints',
  },
  'story-maps': {
    title: 'Story Maps',
    href: '/planning/story-maps',
  },

  ppm: {
    title: 'PPM',
    href: null,
  },
  portfolios: {
    title: 'Portfolios',
    href: '/ppm/portfolios',
  },
  programs: {
    title: 'Programs',
    href: '/ppm/programs',
  },
  projects: {
    title: 'Projects',
    href: '/ppm/projects',
  },
  'strategic-initiatives': {
    title: 'Strategic Initiatives',
    href: '/ppm/strategic-initiatives',
  },

  'strategic-management': {
    title: 'Strategic Management',
    href: null,
  },
  'strategic-themes': {
    title: 'Strategic Themes',
    href: '/strategic-management/strategic-themes',
  },

  work: {
    title: 'Work',
    href: null,
  },
  workspaces: {
    title: 'Workspaces',
  },

  settings: {
    title: 'Settings',
    href: null,
  },
  'background-jobs': {
    title: 'Background Jobs',
    href: null,
  },
  connections: {
    title: 'Connections',
  },
  roles: {
    title: 'Roles',
  },
  users: {
    title: 'Users',
  },
  account: {
    title: 'Account',
    href: null,
  },
}

export const generateRoute = (
  pathname: string,
  lastItemTitleOverride?: string,
): BreadcrumbItem[] => {
  const pathSegments = pathname.split('/').filter((item) => item !== '')
  return pathSegments.map((item, index): BreadcrumbItem => {
    // Set the title of the last path segment to the title passed in from the page
    let titleOverride = {}
    if (lastItemTitleOverride && index === pathSegments.length - 1) {
      titleOverride = { title: lastItemTitleOverride }
    }

    return {
      title: item,
      path: item,
      href: '/' + pathSegments.slice(0, index + 1).join('/'),
      ...((routes as Record<string, unknown>)[item] as object),
      ...titleOverride,
    }
  })
}

export default routes
