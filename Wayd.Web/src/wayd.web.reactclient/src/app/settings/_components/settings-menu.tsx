'use client'

import { Input, Menu } from 'antd'
import { ItemType, MenuItemType } from 'antd/es/menu/interface'
import { usePathname } from 'next/navigation'
import { useMemo, useState } from 'react'
import { findMenuKeysByPathname } from '@/src/app/_components/menu/menu-helper'
import useSettingsMenuItems from './use-settings-menu-items'
import styles from './settings-menu.module.css'

/**
 * An item's display text.
 *
 * A group's label is the plain string; a leaf's is a `<Link>` wrapping it, so
 * the text has to be read through the element. Reading only strings silently
 * matched nothing but group names.
 */
const labelText = (item: ItemType<MenuItemType>): string => {
  const label = (item as { label?: unknown }).label
  if (typeof label === 'string') return label
  const child = (label as { props?: { children?: unknown } })?.props?.children
  return typeof child === 'string' ? child : ''
}

/**
 * Keeps groups whose own name matches, and groups with a matching child —
 * narrowed to just those children, so a filtered group shows only what matched
 * rather than everything under a group whose name happened to hit.
 */
const filterMenu = (
  items: ItemType<MenuItemType>[],
  query: string,
): ItemType<MenuItemType>[] => {
  const needle = query.trim().toLowerCase()
  if (!needle) return items

  const matches = (item: ItemType<MenuItemType>) =>
    labelText(item).toLowerCase().includes(needle)

  return items.reduce((acc: ItemType<MenuItemType>[], item) => {
    if (item == null) return acc
    const children = (item as { children?: ItemType<MenuItemType>[] }).children
    if (!children) {
      if (matches(item)) acc.push(item)
      return acc
    }
    if (matches(item)) {
      acc.push(item)
      return acc
    }
    const hits = children.filter((child) => child != null && matches(child))
    if (hits.length > 0) {
      acc.push({ ...item, children: hits } as ItemType<MenuItemType>)
    }
    return acc
  }, [])
}

/**
 * The settings navigation.
 *
 * Built from the same helpers as the app sider, so settings reads as part of
 * the product rather than a separate one — but with flat, always-open group
 * headings rather than the sider's collapsible submenus. Fifteen items across
 * six groups fits a laptop viewport, and always-open saves a click on every
 * move between groups, which is the common way people work in settings.
 *
 * The filter box covers the long-list case that collapsing would have. It
 * filters the rail in place rather than searching the app — the global search
 * (Ctrl+K) already does that, and a second search surface would compete.
 */
const SettingsMenu = () => {
  const { menuItems, routeKeyMap } = useSettingsMenuItems()
  const pathname = usePathname()
  const [query, setQuery] = useState('')

  const { selectedKeys } = findMenuKeysByPathname(pathname, routeKeyMap)

  const visibleItems = useMemo(
    () => filterMenu(menuItems, query),
    [menuItems, query],
  )

  const searching = query.trim().length > 0

  return (
    <div className={styles.nav}>
      <div className={styles.header}>
        <Input
          allowClear
          size="small"
          placeholder="Find a setting"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          aria-label="Find a setting"
        />
      </div>

      <Menu
        mode="inline"
        items={visibleItems}
        selectedKeys={selectedKeys}
        className={styles.menu}
      />

      {searching && visibleItems.length === 0 && (
        <p className={styles.empty}>No settings match “{query}”.</p>
      )}
    </div>
  )
}

export default SettingsMenu
