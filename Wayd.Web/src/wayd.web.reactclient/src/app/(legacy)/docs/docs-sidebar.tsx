'use client'

import { Menu } from 'antd'
import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import type { DocNavItem } from '@/src/services/docs'
import type { MenuProps } from 'antd'

type MenuItem = Required<MenuProps>['items'][number]

interface DocsSidebarProps {
  navigation: DocNavItem[]
  onNavigate?: () => void
}

function buildMenuItems(items: DocNavItem[]): MenuItem[] {
  return items.map((item) => {
    const key = `/docs/${item.slug}`
    if (item.children && item.children.length > 0) {
      return {
        key,
        label: <Link href={key}>{item.title}</Link>,
        children: buildMenuItems(item.children),
      }
    }
    return {
      key,
      label: item.title,
    }
  })
}

function findOpenKeys(
  items: DocNavItem[],
  pathname: string,
  parentKeys: string[] = [],
): string[] {
  for (const item of items) {
    const key = `/docs/${item.slug}`
    if (pathname.startsWith(key)) {
      if (item.children) {
        return [
          ...parentKeys,
          key,
          ...findOpenKeys(item.children, pathname, [...parentKeys, key]),
        ]
      }
      return parentKeys
    }
  }
  return []
}

export default function DocsSidebar({
  navigation,
  onNavigate,
}: DocsSidebarProps) {
  const pathname = usePathname()
  const router = useRouter()

  const menuItems = (() => {
    const homeItem: MenuItem = {
      key: '/docs',
      label: 'Welcome',
    }
    return [homeItem, ...buildMenuItems(navigation)]
  })()

  const defaultOpenKeys = findOpenKeys(navigation, pathname)

  const handleClick: MenuProps['onClick'] = ({ key }) => {
    router.push(key)
    onNavigate?.()
  }

  return (
    <Menu
      mode="inline"
      selectedKeys={[pathname]}
      defaultOpenKeys={defaultOpenKeys}
      items={menuItems}
      onClick={handleClick}
      style={{ height: '100%', borderRight: 0 }}
    />
  )
}
