'use client'

import { ItemType, MenuItemType } from 'antd/es/menu/interface'
import useAuth from '@/src/components/contexts/auth'
import { useFeatureFlag } from '@/src/hooks'
import {
  buildRouteKeyMap,
  filterAndTransformMenuItem,
  Item,
  menuItem,
  MenuItem,
  restrictedMenuSection,
  restrictedPermissionMenuItem,
} from '@/src/app/_components/menu/menu-helper'

interface SettingsMenuOptions {
  planningPoker: boolean
  productManagement: boolean
}

/**
 * Groups ordered most- to least-visited.
 *
 * The previous nine spent four headings on a single link each, so more than
 * half the rail's height was headings rather than destinations. Scoring Models
 * joins PPM (a scoring model exists to rank projects and is set per portfolio)
 * and Connections joins System (platform infrastructure, like feature flags
 * and the job queue).
 *
 * A group is a `restrictedMenuSection`, so it disappears entirely when the
 * viewer can see none of its children — a heading over nothing is worse than
 * no heading.
 */
const buildSettingsMenuItems = (
  options: SettingsMenuOptions,
): (Item | MenuItem)[] => [
  restrictedMenuSection('Access', 'access', undefined, undefined, [
    restrictedPermissionMenuItem(
      'Permissions.Users.View',
      'Users',
      'access.users',
      '/settings/user-management/users',
    ),
    restrictedPermissionMenuItem(
      'Permissions.Roles.View',
      'Roles',
      'access.roles',
      '/settings/user-management/roles',
    ),
    restrictedPermissionMenuItem(
      'Permissions.OidcProviders.View',
      'Identity Providers',
      'access.identity-providers',
      '/settings/auth/providers',
    ),
  ]),

  restrictedMenuSection(
    'Organization',
    'organization',
    undefined,
    undefined,
    [
      restrictedPermissionMenuItem(
        'Permissions.TeamMemberRoles.View',
        'Team Member Roles',
        'organization.team-member-roles',
        '/settings/organization/team-member-roles',
      ),
    ],
  ),

  ...(options.planningPoker
    ? [
        restrictedMenuSection(
          'Planning',
          'planning',
          undefined,
          undefined,
          [
            restrictedPermissionMenuItem(
              'Permissions.EstimationScales.View',
              'Estimation Scales',
              'planning.estimation-scales',
              '/settings/planning/estimation-scales',
            ),
          ],
        ),
      ]
    : []),

  ...(options.productManagement
    ? [
        restrictedMenuSection(
          'Product Management',
          'product-management',
          undefined,
          undefined,
          [
            restrictedPermissionMenuItem(
              'Permissions.ProductTagCategories.View',
              'Product Tags',
              'product-management.product-tags',
              '/settings/product-management/product-tags',
            ),
          ],
        ),
      ]
    : []),

  restrictedMenuSection('PPM', 'ppm', undefined, undefined, [
    restrictedPermissionMenuItem(
      'Permissions.ExpenditureCategories.View',
      'Expenditure Categories',
      'ppm.expenditure-categories',
      '/settings/ppm/expenditure-categories',
    ),
    restrictedPermissionMenuItem(
      'Permissions.ProjectLifecycles.View',
      'Project Lifecycles',
      'ppm.project-lifecycles',
      '/settings/ppm/project-lifecycles',
    ),
    restrictedPermissionMenuItem(
      'Permissions.ScoringModels.View',
      'Scoring Models',
      'ppm.scoring-models',
      '/settings/scoring/scoring-models',
    ),
  ]),

  ...(options.productManagement
    ? [
        restrictedMenuSection('Delivery', 'delivery', undefined, undefined, [
          restrictedPermissionMenuItem(
            'Permissions.DeploymentEnvironments.View',
            'Environments',
            'delivery.environments',
            '/settings/delivery/environments',
          ),
        ]),
      ]
    : []),

  // Work types, statuses and processes carry no View permission of their own,
  // so they are plain items — the group is reachable by anyone who can open
  // settings at all.
  menuItem(
    'Work Management',
    'work-management',
    undefined,
    undefined,
    [
      menuItem(
        'Work Types',
        'work-management.work-types',
        '/settings/work-management/work-types',
      ),
      menuItem(
        'Work Statuses',
        'work-management.work-statuses',
        '/settings/work-management/work-statuses',
      ),
      menuItem(
        'Work Processes',
        'work-management.work-processes',
        '/settings/work-management/work-processes',
      ),
    ],
  ),

  restrictedMenuSection('System', 'system', undefined, undefined, [
    restrictedPermissionMenuItem(
      'Permissions.FeatureFlags.View',
      'Feature Flags',
      'system.feature-flags',
      '/settings/feature-management/feature-flags',
    ),
    restrictedPermissionMenuItem(
      'Permissions.StatusWorkflows.View',
      'Status Workflows',
      'system.status-workflows',
      '/settings/status-workflows',
    ),
    restrictedPermissionMenuItem(
      'Permissions.Connections.View',
      'Connections',
      'system.connections',
      '/settings/connections',
    ),
    restrictedPermissionMenuItem(
      'Permissions.BackgroundJobs.View',
      'Background Jobs',
      'system.background-jobs',
      '/settings/background-jobs',
    ),
    restrictedPermissionMenuItem(
      'Permissions.Messaging.View',
      'Messaging',
      'system.messaging',
      '/settings/messaging',
    ),
  ]),
]

/**
 * Turns each top-level entry into a non-collapsible group heading.
 *
 * The shared transform builds submenus, which is right for the app sider —
 * that nav is deep and collapsing keeps it short. Settings is fifteen items,
 * so always-open costs about twenty rows (a laptop viewport holds it) and
 * saves a click on every move between groups, which is the common case here.
 * The filter box covers the rare long-list moment that collapsing was for.
 */
const asGroups = (
  items: ItemType<MenuItemType>[],
): ItemType<MenuItemType>[] =>
  items.map((item) =>
    item != null && 'children' in item
      ? ({ ...item, type: 'group' } as ItemType<MenuItemType>)
      : item,
  )

/**
 * The first page the viewer can actually open, for `/settings` to land on.
 *
 * Read off the filtered menu rather than hardcoded, so it follows the same
 * permission and feature-flag rules the rail does — a viewer who cannot see
 * Users is sent to whatever their first group holds instead of bounced.
 *
 * Undefined when the whole rail is empty.
 */
export const firstSettingsRoute = (
  items: ItemType<MenuItemType>[],
): string | undefined => {
  for (const item of items) {
    if (item == null) continue
    const children = (item as { children?: ItemType<MenuItemType>[] }).children
    if (children) {
      const nested = firstSettingsRoute(children)
      if (nested) return nested
      continue
    }
    const route = (item as { route?: string }).route
    if (route) return route
  }
  return undefined
}

/**
 * The settings menu's items and its route→key map, built from the same helpers
 * as the app sider's so the two navs stay one kind of object rather than two.
 */
export const useSettingsMenuItems = () => {
  const { hasClaim } = useAuth()
  const { isEnabled: planningPoker } = useFeatureFlag('planning-poker')
  const { isEnabled: productManagement } = useFeatureFlag('product-management')

  const items = buildSettingsMenuItems({ planningPoker, productManagement })

  const menuItems = asGroups(
    items.reduce(
      (acc, item) => filterAndTransformMenuItem(acc, item, hasClaim),
      [] as ItemType<MenuItemType>[],
    ),
  )

  const routeKeyMap = buildRouteKeyMap(
    items.filter((item): item is Item => item != null && 'display' in item),
  )

  return { menuItems, routeKeyMap }
}

export default useSettingsMenuItems
