'use client'

import { Layout } from 'antd'
import SettingsMenu from './_components/settings-menu'
import SettingsNavBar from './_components/settings-nav-bar'
import styles from './settings-layout.module.css'

const { Content, Sider } = Layout

/**
 * The settings area's chrome: its navigation, and nothing else — the rail on
 * wide screens, a bar that opens it in a drawer on narrow ones.
 *
 * There is deliberately no `PageTitle "Settings"` above the rail. Every page
 * inside carries its own title, so the heading only ever repeated what the
 * page already said and what the app sider's active gear already showed — and
 * on a record page it would stack a third heading above the identity bar.
 */
const SettingsLayout = ({ children }: { children: React.ReactNode }) => (
  <Layout className={styles.layout}>
    <Sider className={styles.sider} theme="light" width={235}>
      <SettingsMenu />
    </Sider>
    <Content className={styles.content}>
      <div className={styles.compactNav}>
        <SettingsNavBar />
      </div>
      {children}
    </Content>
  </Layout>
)

export default SettingsLayout
