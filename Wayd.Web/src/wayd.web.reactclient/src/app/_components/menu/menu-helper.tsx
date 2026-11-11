import { MenuProps } from 'antd'
import type { ItemType, MenuItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { Key, ReactNode } from 'react'

export type MenuItem = Required<MenuProps>['items'][number]

export interface Item {
  display: string
  key: string
  route?: string
  icon?: ReactNode
  children?: (Item | MenuItem)[]
  authClaimValue?: string
  authClaimType?: string
  restrictedSection?: boolean
}

export const menuItem = (
  display: string,
  key: string,
  route?: string,
  icon?: ReactNode,
  children?: (Item | MenuItem)[],
  restrictedSection?: boolean,
) => {
  return {
    display,
    key,
    route,
    icon,
    children,
    restrictedSection,
  }
}

export const restrictedMenuSection = (
  display: string,
  key: string,
  route?: string,
  icon?: ReactNode,
  children?: (Item | MenuItem)[],
) => {
  return {
    ...menuItem(display, key, route, icon, children, true),
  }
}

export const restrictedMenuItem = (
  authClaimValue: string,
  authClaimType: string,
  display: string,
  key: string,
  route?: string,
  icon?: ReactNode,
  children?: (Item | MenuItem)[],
) => {
  return {
    ...menuItem(display, key, route, icon, children),
    authClaimValue,
    authClaimType,
  }
}

export const restrictedPermissionMenuItem = (
  authClaimValue: string,
  display: string,
  key: string,
  route?: string,
  icon?: ReactNode,
  children?: Item[],
) => {
  return {
    ...menuItem(display, key, route, icon, children),
    authClaimValue,
    authClaimType: 'Permission',
  }
}

function getItem(
  label: string,
  key: Key,
  route?: string,
  icon?: ReactNode,
  children?: MenuItem[],
  type?: 'group',
): MenuItem {
  return {
    key,
    icon,
    children,
    label: route ? <Link href={route}>{label}</Link> : label,
    type,
    title: null,
    route, // Attach route as custom property for navigation
  } as MenuItem
}

export const buildRouteKeyMap = (items: Item[]): Map<string, string> => {
  const map = new Map<string, string>()
  for (const item of items) {
    if (item.route) {
      map.set(item.route, item.key)
    }
    if (item.children) {
      const childItems = item.children.filter(
        (child): child is Item => child != null && 'display' in child,
      )
      const childMap = buildRouteKeyMap(childItems)
      childMap.forEach((value, key) => map.set(key, value))
    }
  }
  return map
}

const countSharedSegments = (pathname: string, route: string): number => {
  const pathSegments = pathname.split('/').filter(Boolean)
  const routeSegments = route.split('/').filter(Boolean)
  let count = 0
  for (let i = 0; i < Math.min(pathSegments.length, routeSegments.length); i++) {
    if (pathSegments[i] === routeSegments[i]) {
      count++
    } else {
      break
    }
  }
  return count
}

export const findMenuKeysByPathname = (
  pathname: string,
  routeKeyMap: Map<string, string>,
): { selectedKeys: string[]; openKeys: string[] } => {
  let bestScore = 0
  let bestKey = ''
  let exactMatch = false

  for (const [route, key] of routeKeyMap) {
    if (route === '/') {
      if (pathname === '/') {
        return { selectedKeys: [key], openKeys: [] }
      }
      continue
    }

    // Exact match or full route prefix match (e.g. /ppm/projects matches /ppm/projects/X)
    if (pathname === route || pathname.startsWith(route + '/')) {
      const score = route.split('/').filter(Boolean).length
      if (!exactMatch || score > bestScore) {
        bestScore = score
        bestKey = key
        exactMatch = true
      }
    } else if (!exactMatch) {
      // Segment-based match (e.g. /organizations/team-of-teams → /organizations/teams)
      const shared = countSharedSegments(pathname, route)
      if (shared > 0 && shared > bestScore) {
        bestScore = shared
        bestKey = key
      }
    }
  }

  if (!bestKey) {
    return { selectedKeys: [], openKeys: [] }
  }

  const openKeys: string[] = []
  const parts = bestKey.split('.')
  if (parts.length > 1) {
    openKeys.push(parts[0])
  }

  return { selectedKeys: [bestKey], openKeys }
}

export const filterAndTransformMenuItem = (
  acc: ItemType<MenuItemType>[],
  item: Item | MenuItem,
  claimCheck: (claimType: string, claimValue: string) => boolean,
): ItemType<MenuItemType>[] => {
  if (item == null) return acc
  if ('type' in item) {
    // For items like dividers that already have a type.
    acc.push(item)
  } else {
    const menuItemItem = item as Item
    // Safely access children now.
    const children = menuItemItem.children
      ? menuItemItem.children.reduce(
          (childAcc, child) =>
            child != null
              ? filterAndTransformMenuItem(childAcc, child, claimCheck)
              : childAcc,
          [] as ItemType<MenuItemType>[],
        )
      : undefined

    if ('authClaimValue' in menuItemItem && menuItemItem.authClaimType) {
      if (claimCheck(menuItemItem.authClaimType, menuItemItem.authClaimValue!)) {
        if (
          menuItemItem.restrictedSection &&
          (!children || children.length === 0)
        ) {
          // Skip the top-level restricted section if no children pass the check.
        } else {
          acc.push(
            getItem(
              menuItemItem.display,
              menuItemItem.key,
              menuItemItem.route,
              menuItemItem.icon,
              children,
            ),
          )
        }
      }
    } else if ('display' in menuItemItem) {
      if (
        menuItemItem.restrictedSection &&
        (!children || children.length === 0)
      ) {
        // Skip the top-level restricted section if no children pass the check.
      } else {
        acc.push(
          getItem(
            menuItemItem.display,
            menuItemItem.key,
            menuItemItem.route,
            menuItemItem.icon,
            children,
          ),
        )
      }
    }
  }
  return acc
}
