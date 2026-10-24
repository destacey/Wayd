'use client'

import { Avatar, Dropdown, Grid, MenuProps, Space, Typography } from 'antd'
import {
  BgColorsOutlined,
  EyeOutlined,
  FileTextOutlined,
  LogoutOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import useAuth from '../../components/contexts/auth'
import { useFeatureFlag } from '../../hooks'
import ThemeManagerDrawer from './theme-manager-drawer'

const { Text } = Typography
const { useBreakpoint } = Grid

const ProfileMenu = () => {
  const { logout, user } = useAuth()
  const router = useRouter()
  const screens = useBreakpoint()
  const isXs = !screens.sm
  const [themeDrawerOpen, setThemeDrawerOpen] = useState(false)
  const { isEnabled: isFollowingEnabled } = useFeatureFlag('following')

  const handleLogout = async () => {
    try {
      await logout()
    } catch (e) {
      console.error(`logoutRedirect failed: ${e}`)
    }
  }

  const menuItems: MenuProps['items'] = [
    {
      key: 'profile',
      label: 'Account',
      icon: <UserOutlined />,
      onClick: () => router.push('/account/profile'),
    },
    ...(isFollowingEnabled && user?.employeeId
      ? [
          {
            key: 'following',
            label: 'Following',
            icon: <EyeOutlined />,
            onClick: () => router.push('/account/following'),
          },
        ]
      : []),
    {
      key: 'theme',
      label: 'Theme',
      icon: <BgColorsOutlined />,
      onClick: () => setThemeDrawerOpen(true),
    },
    {
      key: 'divider',
      type: 'divider',
    },
    ...(process.env.NEXT_PUBLIC_API_BASE_URL
      ? [
          {
            key: 'api-spec',
            label: 'API Specification',
            icon: <FileTextOutlined />,
            onClick: () =>
              window.open(
                `${process.env.NEXT_PUBLIC_API_BASE_URL}/scalar`,
                '_blank',
                'noopener,noreferrer',
              ),
          },
        ]
      : []),
    {
      key: 'logout',
      label: 'Sign Out',
      icon: <LogoutOutlined />,
      onClick: handleLogout,
    },
  ]

  return (
    <>
      <Space>
        {user?.name && !isXs && <Text>Welcome, {user.name}</Text>}
        <Dropdown menu={{ items: menuItems }} trigger={['click']}>
          <Avatar icon={<UserOutlined />} />
        </Dropdown>
      </Space>
      <ThemeManagerDrawer
        open={themeDrawerOpen}
        onClose={() => setThemeDrawerOpen(false)}
      />
    </>
  )
}

export default ProfileMenu
