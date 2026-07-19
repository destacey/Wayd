'use client'

import {
  ItemType,
  MenuItemGroupType,
  MenuItemType,
  SubMenuType,
} from 'antd/es/menu/interface'
import useAuth from '../../../components/contexts/auth'
import { Menu } from 'antd'
import Link from 'next/link'
import { useFeatureFlag } from '../../../hooks'

enum SettingsTab {
  // user-management
  Users = 'users',
  Roles = 'roles',
  IdentityProviders = 'identity-providers',

  // feature management
  FeatureFlags = 'feature-flags',

  // planning
  EstimationScales = 'estimation-scales',

  // project portfolio management
  ExpenditureCategories = 'expenditure-categories',
  ProjectLifecycles = 'project-lifecycles',
  ScoringModels = 'scoring-models',

  // work-management
  WorkTypes = 'work-types',
  WorkStatuses = 'work-statuses',
  WorkProcesses = 'work-processes',

  // organization
  TeamMemberRoles = 'team-member-roles',

  // integrations
  Connections = 'connections',

  // other
  BackgroundJobs = 'background-jobs',
  Messaging = 'messaging',
}

type RestrictedMenuItem = SubMenuType & {
  claimValue: string
}

interface SectionMenuItem extends MenuItemGroupType {}

const authorizeMenuItems = (
  acc: ItemType<MenuItemType>[],
  item: SectionMenuItem | RestrictedMenuItem | SubMenuType,
  claimCheck: (claimValue: string) => boolean,
) => {
  if ('type' in item) {
    const children = item.children
      ? (item.children as (RestrictedMenuItem | SubMenuType)[]).reduce(
          (acc, item) =>
            authorizeMenuItems(acc, item, claimCheck),
          [] as ItemType<MenuItemType>[],
        )
      : undefined
    if (item.type === 'group' && Array.isArray(children) && children.length > 0) {
      acc.push({ ...item, children } as unknown as ItemType<MenuItemType>)
    }
  } else if ('claimValue' in item) {
    if (claimCheck(item.claimValue)) {
      // Filter out claimValue before pushing the item
      const { claimValue, ...rest } = item
      acc.push(rest)
    }
  } else {
    acc.push(item)
  }
  return acc
}

const getSectionMenuItem = (
  label: React.ReactNode,
  key: React.Key,
  children?: SubMenuType[],
): SectionMenuItem => {
  return {
    label,
    key,
    children,
    type: 'group',
  } as SectionMenuItem
}

const getRestrictedMenuItem = (
  claimValue: string,
  label: string,
  key: string,
  route?: string,
  children?: SubMenuType[],
): RestrictedMenuItem => {
  return {
    label: route ? <Link href={route}>{label}</Link> : label,
    key,
    route,
    children,
    claimValue,
  } as RestrictedMenuItem
}

const getRegularMenuItem = (
  label: React.ReactNode,
  key: string,
  route?: string,
  children?: SubMenuType[],
): SubMenuType => {
  return {
    label: route ? <Link href={route}>{label}</Link> : label,
    key,
    route,
    children,
  } as SubMenuType
}

const buildSettingsMenuItems = (featureFlags: {
  planningPoker: boolean
}): ItemType[] => [
  getSectionMenuItem('User Management', 'user-management', [
    getRestrictedMenuItem(
      'Permissions.Users.View',
      'Users',
      SettingsTab.Users,
      '/settings/user-management/users',
    ),
    getRestrictedMenuItem(
      'Permissions.Roles.View',
      'Roles',
      SettingsTab.Roles,
      '/settings/user-management/roles',
    ),
    getRestrictedMenuItem(
      'Permissions.OidcProviders.View',
      'Identity Providers',
      SettingsTab.IdentityProviders,
      '/settings/auth/providers',
    ),
  ]),

  getSectionMenuItem('Feature Management', 'feature-management', [
    getRestrictedMenuItem(
      'Permissions.FeatureFlags.View',
      'Feature Flags',
      SettingsTab.FeatureFlags,
      '/settings/feature-management/feature-flags',
    ),
  ]),

  getSectionMenuItem('Organization', 'organization', [
    getRestrictedMenuItem(
      'Permissions.TeamMemberRoles.View',
      'Team Member Roles',
      SettingsTab.TeamMemberRoles,
      '/settings/organization/team-member-roles',
    ),
  ]),

  ...(featureFlags.planningPoker
    ? [
        getSectionMenuItem('Planning', 'planning', [
          getRestrictedMenuItem(
            'Permissions.EstimationScales.View',
            'Estimation Scales',
            SettingsTab.EstimationScales,
            '/settings/planning/estimation-scales',
          ),
        ]),
      ]
    : []),

  getSectionMenuItem('Work Management', 'work-management', [
    getRegularMenuItem(
      'Work Types',
      SettingsTab.WorkTypes,
      '/settings/work-management/work-types',
    ),
    getRegularMenuItem(
      'Work Statuses',
      SettingsTab.WorkStatuses,
      '/settings/work-management/work-statuses',
    ),
    getRegularMenuItem(
      'Work Processes',
      SettingsTab.WorkProcesses,
      '/settings/work-management/work-processes',
    ),
  ]),

  getSectionMenuItem('PPM', 'ppm', [
    getRestrictedMenuItem(
      'Permissions.ExpenditureCategories.View',
      'Expenditure Categories',
      SettingsTab.ExpenditureCategories,
      '/settings/ppm/expenditure-categories',
    ),
    getRestrictedMenuItem(
      'Permissions.ProjectLifecycles.View',
      'Project Lifecycles',
      SettingsTab.ProjectLifecycles,
      '/settings/ppm/project-lifecycles',
    ),
  ]),

  getSectionMenuItem('Scoring', 'scoring', [
    getRestrictedMenuItem(
      'Permissions.ScoringModels.View',
      'Scoring Models',
      SettingsTab.ScoringModels,
      '/settings/scoring/scoring-models',
    ),
  ]),

  getSectionMenuItem('Integrations', 'integration-management', [
    getRestrictedMenuItem(
      'Permissions.Connections.View',
      'Connections',
      SettingsTab.Connections,
      '/settings/connections',
    ),
  ]),

  getSectionMenuItem('Other', 'other', [
    getRestrictedMenuItem(
      'Permissions.BackgroundJobs.View',
      'Background Jobs',
      SettingsTab.BackgroundJobs,
      '/settings/background-jobs',
    ),
    getRestrictedMenuItem(
      'Permissions.Messaging.View',
      'Messaging',
      SettingsTab.Messaging,
      '/settings/messaging',
    ),
  ]),
]

// TODO: improve style and layout for smaller screens
export default function SettingsMenu() {
  const { hasPermissionClaim } = useAuth()
  const { isEnabled: planningPoker } = useFeatureFlag('planning-poker')

  // Derive menu items based on user's permissions and feature flags
  const menuItems = (buildSettingsMenuItems({ planningPoker }) as SectionMenuItem[]).reduce(
      (acc: ItemType<MenuItemType>[], item: SectionMenuItem) =>
        authorizeMenuItems(acc, item, hasPermissionClaim),
      [] as ItemType<MenuItemType>[],
    )

  return (
    <Menu
      mode="inline"
      style={{
        borderRight: 0,
      }}
      items={menuItems as unknown as ItemType[]}
    />
  )
}
