'use client'

import { FC, memo, useMemo } from 'react'
import useMenuToggle from '../../../components/contexts/menu-toggle'
import useTheme from '../../../components/contexts/theme'
import { Layout } from 'antd'
import AppMenu from './app-menu'

const { Sider } = Layout

interface AppSideNavProps {
  isMobile?: boolean
}

const AppSideNav: FC<AppSideNavProps> = memo(
  ({ isMobile = false }: AppSideNavProps) => {
    const { menuCollapsed } = useMenuToggle()
    const { token } = useTheme()
    const menuTheme = useMemo<'light' | 'dark'>(() => {
      const hex = token.colorBgContainer.trim()
      const match = /^#([0-9a-f]{6})$/i.exec(hex)
      if (!match) return 'light'

      const value = match[1]
      const r = Number.parseInt(value.slice(0, 2), 16)
      const g = Number.parseInt(value.slice(2, 4), 16)
      const b = Number.parseInt(value.slice(4, 6), 16)
      const luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255

      return luminance < 0.5 ? 'dark' : 'light'
    }, [token.colorBgContainer])

    // Return null for mobile since the dropdown is handled in AppHeader
    if (isMobile) {
      return null
    }

    return (
      <Sider
        className="app-side-nav"
        theme={menuTheme}
        width={235}
        collapsedWidth={50}
        collapsed={menuCollapsed}
      >
        <AppMenu
          theme={menuTheme}
          style={{ minHeight: '100%' }}
        />
      </Sider>
    )
  },
)

AppSideNav.displayName = 'AppSideNav'

export default AppSideNav
