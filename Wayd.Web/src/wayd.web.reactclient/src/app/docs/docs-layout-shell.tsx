'use client'

import { useState } from 'react'
import { Button, Drawer, Grid, Layout } from 'antd'
import { MenuOutlined } from '@ant-design/icons'
import DocsSidebar from './docs-sidebar'
import type { DocNavItem } from '@/src/services/docs'

const { Sider, Content } = Layout
const { useBreakpoint } = Grid

interface DocsLayoutShellProps {
  navigation: DocNavItem[]
  children: React.ReactNode
}

export default function DocsLayoutShell({
  navigation,
  children,
}: DocsLayoutShellProps) {
  const screens = useBreakpoint()
  const isMobile = !screens.md
  const [drawerOpen, setDrawerOpen] = useState(false)

  if (isMobile) {
    return (
      <Layout className="docs-layout docs-layout-mobile">
        <div className="docs-mobile-menu-bar">
          <Button
            type="text"
            icon={<MenuOutlined />}
            onClick={() => setDrawerOpen(true)}
          >
            Docs Menu
          </Button>
        </div>
        <Drawer
          title="Documentation"
          placement="left"
          onClose={() => setDrawerOpen(false)}
          open={drawerOpen}
          size={280}
          styles={{ body: { padding: 0 } }}
        >
          <DocsSidebar
            navigation={navigation}
            onNavigate={() => setDrawerOpen(false)}
          />
        </Drawer>
        <Content className="docs-mobile-content">{children}</Content>
      </Layout>
    )
  }

  return (
    <Layout className="docs-layout">
      <Sider width={260} theme="light">
        <DocsSidebar navigation={navigation} />
      </Sider>
      <Content
        style={{
          padding: '24px',
          maxWidth: '900px',
        }}
      >
        {children}
      </Content>
    </Layout>
  )
}

