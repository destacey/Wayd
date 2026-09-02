'use client'

import {
  TeamOutlined,
  HomeOutlined,
  SettingOutlined,
  ScheduleOutlined,
  CarryOutOutlined,
  ProjectOutlined,
  FundOutlined,
  ProductOutlined,
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
import { useFeatureFlag, useLinkedEmployee } from '../../../hooks'

const menuIcons = {
  home: <HomeOutlined />,
  org: <TeamOutlined />,
  planning: <ScheduleOutlined />,
  ppm: <ProjectOutlined />,
  product: <ProductOutlined />,
  strategy: <FundOutlined />,
  work: <CarryOutOutlined />,
  settings: <SettingOutlined />,
}

interface MenuOptions {
  planningPoker: boolean
  storyMaps: boolean
  productManagement: boolean
  /**
   * Whether the signed-in account is linked to an employee record. Personal views are keyed on the
   * employee, so they are omitted entirely for an unlinked account rather than offered and empty.
   */
  hasLinkedEmployee: boolean
}

const buildMenuItems = (options: MenuOptions): (Item | MenuItem)[] => [
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
    ...(options.planningPoker || options.storyMaps
      ? [{ key: 'settings-planning-divider', type: 'divider' as const }]
      : []),
    ...(options.planningPoker
      ? [
          restrictedPermissionMenuItem(
            'Permissions.PokerSessions.View',
            'Planning Poker',
            'plan.poker-sessions',
            '/planning/poker-sessions',
          ),
        ]
      : []),
    ...(options.storyMaps
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
  ...(options.productManagement
    ? [
        restrictedMenuSection(
          'Product Management',
          'product',
          undefined,
          menuIcons.product,
          [
            restrictedPermissionMenuItem(
              'Permissions.Products.View',
              'Products',
              'product.products',
              '/product-management/products',
            ),
            // Delivery is schema-separated from the catalog so a later module split stays a code
            // move, but it is one module today and belongs in one section.
            //
            // Ordered as delivery runs: what is cut, what it is bundled into, where it went, and how
            // that went.
            restrictedPermissionMenuItem(
              'Permissions.Releases.View',
              'Releases',
              'delivery.releases',
              '/delivery/releases',
            ),
            restrictedPermissionMenuItem(
              'Permissions.ReleasePackages.View',
              'Release Packages',
              'delivery.release-packages',
              '/delivery/release-packages',
            ),
            restrictedPermissionMenuItem(
              'Permissions.Deployments.View',
              'Deployments',
              'delivery.deployments',
              '/delivery/deployments',
            ),
            restrictedPermissionMenuItem(
              'Permissions.DeliveryMetrics.View',
              'Delivery Metrics',
              'delivery.metrics',
              '/delivery/metrics',
            ),
          ],
        ),
      ]
    : []),
  restrictedMenuSection('PPM', 'ppm', undefined, menuIcons.ppm, [
    // "My Projects" resolves the caller's own project roles, which are held by the employee record.
    // An unlinked account has none, so the page would always be empty — omit it, along with the
    // divider that would otherwise be left leading the section.
    ...(options.hasLinkedEmployee
      ? [
          restrictedPermissionMenuItem(
            'Permissions.Projects.View',
            'My Projects',
            'ppm.dashboards.my-projects',
            '/ppm/dashboards/my-projects',
          ),
          { key: 'ppm-dashboards-divider', type: 'divider' as const },
        ]
      : []),
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
  const { hasLinkedEmployee } = useLinkedEmployee()
  const { isEnabled: planningPoker } = useFeatureFlag('planning-poker')
  const { isEnabled: storyMaps } = useFeatureFlag('story-maps')
  const { isEnabled: productManagement } = useFeatureFlag('product-management')

  const items = buildMenuItems({
    planningPoker,
    storyMaps,
    productManagement,
    hasLinkedEmployee,
  })

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
