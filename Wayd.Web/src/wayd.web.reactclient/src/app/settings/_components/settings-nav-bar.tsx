'use client'

import { MenuOutlined } from '@ant-design/icons'
import { Button, Drawer } from 'antd'
import { usePathname } from 'next/navigation'
import { useState } from 'react'
import SettingsMenu from './settings-menu'
import useSettingsMenuItems, {
  settingsKeyForPath,
  settingsLocation,
} from './use-settings-menu-items'
import styles from './settings-nav-bar.module.css'

/**
 * The settings navigation on screens too narrow for the rail.
 *
 * A bar naming where the viewer is ("Access › Users") opens the same
 * `SettingsMenu` in a drawer, so the groups, the filter box and the
 * permission rules are the rail's own rather than a second copy to keep in
 * step. The layout's CSS decides which of the two is shown.
 */
const SettingsNavBar = () => {
  const { menuItems, routeKeyMap } = useSettingsMenuItems()
  const pathname = usePathname()

  // Remembers the page the drawer was opened on, so navigating by any route —
  // including the browser's back button — closes it without an effect.
  const [openedOn, setOpenedOn] = useState<string | null>(null)
  const open = openedOn === pathname

  const location = settingsLocation(
    menuItems,
    settingsKeyForPath(pathname, routeKeyMap),
  )

  // No aria-label: it would replace the visible location as the button's
  // name, so a screen reader would never say where the viewer is.
  return (
    <div className={styles.bar}>
      <Button
        type="text"
        icon={<MenuOutlined aria-hidden />}
        onClick={() => setOpenedOn(pathname)}
        aria-haspopup="dialog"
        aria-expanded={open}
        className={styles.trigger}
      >
        <span className={styles.location}>
          <span className={styles.visuallyHidden}>Settings menu: </span>
          {location?.group && (
            <span className={styles.group}>{location.group} › </span>
          )}
          {location?.page ?? 'Settings'}
        </span>
      </Button>
      <Drawer
        title="Settings"
        placement="left"
        open={open}
        onClose={() => setOpenedOn(null)}
        size={280}
        styles={{ body: { padding: 0 } }}
      >
        <SettingsMenu onNavigate={() => setOpenedOn(null)} />
      </Drawer>
    </div>
  )
}

export default SettingsNavBar
