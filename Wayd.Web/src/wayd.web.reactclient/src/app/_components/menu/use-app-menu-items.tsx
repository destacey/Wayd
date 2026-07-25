'use client'

import {
  TeamOutlined,
  HomeOutlined,
  SettingOutlined,
  ScheduleOutlined,
  CarryOutOutlined,
  ProjectOutlined,
  FundOutlined,
} from '@ant-design/icons'
import {
  buildRouteKeyMap,
  filterAndTransformMenuItem,
  Item,
  menuItem,
  MenuItem,
  restrictedMenuSection,
  restrictedPermissionMenuItem,
} from './menu-helper'
import { ItemType, MenuItemType } from 'antd/es/menu/interface'
import useAuth from '../../../components/contexts/auth'
import { useFeatureFlag } from '../../../hooks'

const menuIcons = {
  home: <HomeOutlined />,
  org: <TeamOutlined />,
  planning: <ScheduleOutlined />,
  ppm: <ProjectOutlined />,
  strategy: <FundOutlined />,
  work: <CarryOutOutlined />,
  settings: <SettingOutlined />,
}

const buildMenuItems = (featureFlags: {
  planningPoker: boolean
  storyMaps: boolean
}): (Item | MenuItem)[] => [
  menuItem('Home', 'home', '/', menuIcons.home),
  menuItem('Organizations', 'org', undefined, menuIcons.org, [
    menuItem('Teams', 'org.teams', '/organizations/teams'),
    menuItem('Employees', 'org.employees', '/organizations/employees'),
    { key: 'org-settings-divider-1', type: 'divider' },
    menuItem(
      'Functional Org Chart',
      'org.functional-org-chart',
      '/organizations/functional-org-chart',
    ),
  ]),
  restrictedMenuSection('Planning', 'plan', undefined, menuIcons.planning, [
    restrictedPermissionMenuItem(
      'Permissions.PlanningIntervals.View',
      'Planning Intervals',
      'plan.planning-intervals',
      '/planning/planning-intervals',
    ),
    restrictedPermissionMenuItem(
      'Permissions.Iterations.View',
      'Sprints',
      'plan.sprints',
      '/planning/sprints',
    ),
    restrictedPermissionMenuItem(
      'Permissions.Roadmaps.View',
      'Roadmaps',
      'plan.roadmaps',
      '/planning/roadmaps',
    ),
    ...(featureFlags.planningPoker || featureFlags.storyMaps
      ? [{ key: 'settings-planning-divider', type: 'divider' as const }]
      : []),
    ...(featureFlags.planningPoker
      ? [
          restrictedPermissionMenuItem(
            'Permissions.PokerSessions.View',
            'Planning Poker',
            'plan.poker-sessions',
            '/planning/poker-sessions',
          ),
        ]
      : []),
    ...(featureFlags.storyMaps
      ? [
          restrictedPermissionMenuItem(
            'Permissions.StoryMaps.View',
            'Story Maps',
            'plan.story-maps',
            '/planning/story-maps',
          ),
        ]
      : []),
  ]),
  restrictedMenuSection('Work Management', 'work', undefined, menuIcons.work, [
    restrictedPermissionMenuItem(
      'Permissions.Workspaces.View',
      'Workspaces',
      'work.workspaces',
      '/work/workspaces',
    ),
  ]),
  // menuItem('Products', 'pdc', null, <DesktopOutlined />, [
  //     menuItem('Product Lines', 'pdc.product-lines'),
  //     menuItem('Product Types', 'pdc.product-types'),
  //     menuItem('Products', 'pdc.products'),
  //     { type: 'divider' },
  //     menuItem('Releases', 'pdc.releases'),
  //     menuItem('Roadmaps', 'pdc.roadmaps'),
  //     { type: 'divider' },
  //     menuItem('Requirements Management', 'pdc.requirements-management'),
  // ]),
  restrictedMenuSection('PPM', 'ppm', undefined, menuIcons.ppm, [
    restrictedPermissionMenuItem(
      'Permissions.Projects.View',
      'My Projects',
      'ppm.dashboards.my-projects',
      '/ppm/dashboards/my-projects',
    ),
    { key: 'ppm-dashboards-divider', type: 'divider' },
    restrictedPermissionMenuItem(
      'Permissions.ProjectPortfolios.View',
      'Portfolios',
      'ppm.portfolios',
      '/ppm/portfolios',
    ),
    restrictedPermissionMenuItem(
      'Permissions.Programs.View',
      'Programs',
      'ppm.programs',
      '/ppm/programs',
    ),
    restrictedPermissionMenuItem(
      'Permissions.Projects.View',
      'Projects',
      'ppm.projects',
      '/ppm/projects',
    ),
    restrictedPermissionMenuItem(
      'Permissions.StrategicInitiatives.View',
      'Strategic Initiatives',
      'ppm.strategic-initiatives',
      '/ppm/strategic-initiatives',
    ),
    { key: 'settings-ppm-divider', type: 'divider' },
    restrictedPermissionMenuItem(
      'Permissions.StrategicThemes.View',
      'Strategic Themes',
      'strategy.strategic-themes',
      '/strategic-management/strategic-themes',
    ),
  ]),
  // restrictedMenuSection(
  //   'Strategic Management',
  //   'strategy',
  //   null,
  //   menuIcons.strategy,
  //   [
  //     restrictedPermissionMenuItem(
  //       'Permissions.StrategicThemes.View',
  //       'Strategic Themes',
  //       'strategy.strategic-themes',
  //       '/strategic-management/strategic-themes',
  //     ),
  //   ],
  // ),
  { key: 'settings-divider', type: 'divider' },
  menuItem('Settings', 'settings', '/settings', menuIcons.settings),
]

const useAppMenuItems = () => {
  const { hasClaim } = useAuth()
  const { isEnabled: planningPoker } = useFeatureFlag('planning-poker')
  const { isEnabled: storyMaps } = useFeatureFlag('story-maps')

  const items = buildMenuItems({ planningPoker, storyMaps })

  const filteredMenuItems = items.reduce(
    (acc, item) =>
      item != null ? filterAndTransformMenuItem(acc, item, hasClaim) : acc,
    [] as ItemType<MenuItemType>[],
  )

  const routeKeyMap = buildRouteKeyMap(
    items.filter((item): item is Item => item != null && 'display' in item),
  )

  return { menuItems: filteredMenuItems, routeKeyMap }
}

export default useAppMenuItems
