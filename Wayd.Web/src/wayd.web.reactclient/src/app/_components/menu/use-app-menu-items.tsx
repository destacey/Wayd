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
            // move, but it is one module today and belongs in one section — which is why these route
            // under `/product-management/`, matching both this section and the API, where every one
            // of these endpoints already lives under `api/product-management/`. "Delivery" names the
            // schema and the concept, not a URL namespace.
            //
            // Releases lead, then the engineering chain ordered as delivery runs: what was cut, what
            // it was bundled into, where it went, and how that went. The announcement is what a
            // product manager opens the section for, and it reads as the whole the rest feeds.
            //
            // Guarded on its own permission rather than Delivery's: a product manager drafting
            // 2026.07 is a different person from whoever records that the pipeline ran.
            restrictedPermissionMenuItem(
              'Permissions.Releases.View',
              'Releases',
              'product.releases',
              '/product-management/releases',
            ),
            { key: 'settings-product-divider', type: 'divider' },
            // Leads the engineering chain rather than the whole section: it summarises what the
            // items below record, so it reads as their overview rather than as a sibling of the
            // catalog. Guarded on Delivery — it reads version records, not products.
            restrictedPermissionMenuItem(
              'Permissions.Delivery.View',
              'Delivery',
              'product.delivery',
              '/product-management/delivery',
            ),
            restrictedPermissionMenuItem(
              'Permissions.Delivery.View',
              'Versions',
              'product.versions',
              '/product-management/versions',
            ),
            restrictedPermissionMenuItem(
              'Permissions.Delivery.View',
              'Release Packages',
              'product.release-packages',
              '/product-management/release-packages',
            ),
            restrictedPermissionMenuItem(
              'Permissions.Delivery.View',
              'Deployments',
              'product.deployments',
              '/product-management/deployments',
            ),
            restrictedPermissionMenuItem(
              'Permissions.DeliveryMetrics.View',
              'Delivery Metrics',
              'product.metrics',
              '/product-management/metrics',
            ),
            { key: 'product-environments-divider', type: 'divider' },
            // After the chain rather than beside Deployments: these answer "where can it go" and
            // "what is there now", so slotting them between "where it went" and "how that went"
            // would break the sequence the items above are ordered to read as.
            //
            // Rollout is guarded on Delivery rather than the environment claim: it is the deployment
            // record read by environment, and someone who may define targets need not be able to see
            // what shipped to them.
            restrictedPermissionMenuItem(
              'Permissions.Delivery.View',
              'Rollout',
              'product.rollout',
              '/product-management/rollout',
            ),
            restrictedPermissionMenuItem(
              'Permissions.DeploymentEnvironments.View',
              'Environments',
              'product.environments',
              '/product-management/environments',
            ),
          ],
        ),
      ]
    : []),
  restrictedMenuSection('PPM', 'ppm', undefined, menuIcons.ppm, [
    // Offered to unlinked accounts too: the dashboard's Person scope works without an employee
    // record of one's own, and the page explains why the Me scope is missing.
    restrictedPermissionMenuItem(
      'Permissions.Projects.View',
      'Projects Dashboard',
      'ppm.dashboards.projects',
      '/ppm/dashboards/projects',
    ),
    { key: 'ppm-dashboards-divider', type: 'divider' as const },
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
    // Keyed under `ppm` because that is the section it is rendered in. The key's first segment is
    // what the menu opens on navigation, so a `strategy.` key here would ask to open a section that
    // is not in the tree, and the item would select with its parent collapsed.
    restrictedPermissionMenuItem(
      'Permissions.StrategicThemes.View',
      'Strategic Themes',
      'ppm.strategic-themes',
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
