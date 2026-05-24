'use client'

import {
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  MenuOutlined,
  SearchOutlined,
  ReadOutlined,
} from '@ant-design/icons'
import React, { FC, useState } from 'react'
import {
  Layout,
  Button,
  Typography,
  Flex,
  Drawer,
  Menu,
  MenuProps,
  Grid,
  Tooltip,
} from 'antd'
import useMenuToggle from '../../components/contexts/menu-toggle'
import { useAppMenuItems } from './menu'
import ProfileMenu from './profile-menu'
import GlobalSearchModal from './global-search/global-search-modal'
import { useGlobalSearch } from './global-search/use-global-search'
import { ItemType, MenuItemType } from 'antd/es/menu/interface'
import { useRouter } from 'next/navigation'
import styles from './app-header.module.css'
import useTheme from '@/src/components/contexts/theme'

const { Header } = Layout
const { Title, Text } = Typography
const { useBreakpoint } = Grid

// Menu item type with custom route property
type MenuItemWithRoute = {
  route?: string
  children?: MenuItemWithRoute[]
  [key: string]: any
}

// Flatten nested menu items for mobile display
const flattenMenuItems = (
  items: ItemType<MenuItemType>[],
): ItemType<MenuItemType>[] => {
  const flattened: ItemType<MenuItemType>[] = []

  items.forEach((item) => {
    if (!item || typeof item === 'string') return

    // Skip dividers
    if ('type' in item && item.type === 'divider') return

    const menuItem = item as any

    // If item has children, add as a group with children
    if (menuItem.children && menuItem.children.length > 0) {
      // Add as menu group
      flattened.push({
        type: 'group',
        label: menuItem.label?.props?.children || menuItem.label,
        key: `${menuItem.key}-group`,
        children: menuItem.children,
      })
    } else {
      // Add regular item
      flattened.push(menuItem)
    }
  })

  return flattened
}

const AppHeader: FC = React.memo(() => {
  const { menuCollapsed, setMenuCollapsed } = useMenuToggle()
  const { appBar } = useTheme()
  const screens = useBreakpoint()
  const isMobile = !screens.md // Mobile/tablet for md and below (< 768px)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const { menuItems } = useAppMenuItems()
  const router = useRouter()
  const {
    open: searchOpen,
    openSearch,
    closeSearch,
    consumeRequestedScope,
  } = useGlobalSearch()

  // Flatten menu for mobile
  const mobileMenuItems = flattenMenuItems(menuItems)

  // Avoid re-renders when authentication state changes
  const profileComponent = <ProfileMenu />

  const handleMenuClick: MenuProps['onClick'] = (e) => {
    // Find the clicked item and navigate using the custom route property
    const findItem = (items: any[], key: string): MenuItemWithRoute | null => {
      for (const item of items) {
        if (!item || typeof item === 'string') continue

        if (item.key === key) return item as MenuItemWithRoute

        if (item.children) {
          const found = findItem(item.children, key)
          if (found) return found
        }
      }
      return null
    }

    const clickedItem = findItem(mobileMenuItems, e.key)
    if (clickedItem?.route) {
      router.push(clickedItem.route)
      setDrawerOpen(false)
    }
  }

  return (
    <Header
      style={{
        height: 50,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0 16px',
        backgroundColor: appBar.backgroundColor,
        color: appBar.color,
      }}
    >
      <Flex>
        {isMobile ? (
          <>
            <Button
              type="text"
              icon={<MenuOutlined />}
              size="middle"
              onClick={() => setDrawerOpen(true)}
              aria-label="Open navigation menu"
              style={{ color: appBar.color }}
            />
            <Drawer
              title="Menu"
              placement="left"
              onClose={() => setDrawerOpen(false)}
              open={drawerOpen}
              size={280}
              styles={{
                body: { padding: 0 },
              }}
            >
              <Menu
                mode="inline"
                items={mobileMenuItems}
                onClick={handleMenuClick}
                style={{ border: 'none' }}
              />
            </Drawer>
          </>
        ) : (
          <Button
            type="text"
            icon={menuCollapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            size="middle"
            onClick={() => setMenuCollapsed(!menuCollapsed)}
            aria-label={
              menuCollapsed ? 'Expand sidebar menu' : 'Collapse sidebar menu'
            }
            style={{ color: appBar.color }}
          />
        )}
        <Title
          level={4}
          style={{
            margin: '0 16px',
            fontSize: 24,
            fontWeight: 400,
            whiteSpace: 'nowrap',
            color: appBar.color,
          }}
        >
          wayd
        </Title>
      </Flex>
      <Flex align="center" gap={8}>
        {isMobile ? (
          <Tooltip title="Search (Ctrl+K)">
            <Button
              type="text"
              icon={<SearchOutlined />}
              onClick={openSearch}
              aria-label="Search (Ctrl+K)"
              style={{ color: appBar.color }}
            />
          </Tooltip>
        ) : (
          <Button
            icon={<SearchOutlined />}
            onClick={openSearch}
            aria-label="Search (Ctrl+K)"
            className={styles.searchTrigger}
          >
            <Text style={{ color: appBar.subtleColor ?? appBar.color }}>
              Search...
            </Text>
            <Flex gap={4} className={styles.searchTriggerKbdGroup}>
              <Tooltip title="Search app data">
                <kbd className={styles.searchTriggerKbd}>Ctrl+K</kbd>
              </Tooltip>
              <Tooltip title="Search documentation">
                <kbd className={styles.searchTriggerKbd}>Ctrl+K D</kbd>
              </Tooltip>
            </Flex>
          </Button>
        )}
        <Tooltip title="Documentation">
          <Button
            type="text"
            icon={<ReadOutlined />}
            href="/docs"
            aria-label="Documentation"
            style={{ color: appBar.color }}
          />
        </Tooltip>
        {profileComponent}
      </Flex>
      <GlobalSearchModal
        open={searchOpen}
        onClose={closeSearch}
        consumeRequestedScope={consumeRequestedScope}
      />
    </Header>
  )
})

AppHeader.displayName = 'AppHeader'

export default AppHeader
