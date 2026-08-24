'use client'

import { Card, Divider, Layout } from 'antd'
import { PageTitle } from '@/src/components/common'
import SettingsMenu from './_components/settings-menu'

const { Content, Sider } = Layout

const SettingsLayout = ({ children }: { children: React.ReactNode }) => {
  return (
    <>
      <br />
      <PageTitle title="Settings" />
      <Divider />
      <Layout>
        <Card size="small">
          <Sider>
            <SettingsMenu />
          </Sider>
        </Card>
        <Content style={{ paddingLeft: '24px' }}>{children}</Content>
      </Layout>
    </>
  )
}

export default SettingsLayout
