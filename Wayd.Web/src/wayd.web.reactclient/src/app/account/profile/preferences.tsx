'use client'

import { FC } from 'react'
import { Button, Flex, Popconfirm, Switch, Typography } from 'antd'
import {
  GRID_PERSISTENCE_ENABLED_KEY,
  clearAllGridColumnState,
} from '@/src/components/common/wayd-grid'
import { useLocalStorageState } from '@/src/hooks'
import { useMessage } from '@/src/components/contexts/messaging'

const { Text, Title } = Typography

/**
 * Account-level preferences. Grid column layouts are stored in this browser's
 * localStorage (not on the server), so both controls act on this device only.
 * Disabling keeps existing saved layouts (re-enabling restores them);
 * "Reset all" deletes them.
 */
const Preferences: FC = () => {
  const [rememberColumnLayouts, setRememberColumnLayouts] =
    useLocalStorageState<boolean>(GRID_PERSISTENCE_ENABLED_KEY, true)
  const messageApi = useMessage()

  const onResetAllGridLayouts = () => {
    clearAllGridColumnState()
    messageApi.success('All saved grid column layouts have been reset.')
  }

  return (
    <Flex vertical gap="large" style={{ maxWidth: 640 }}>
      <div>
        <Title level={5}>Grids</Title>
        <Text type="secondary">
          Grid column layouts are saved in this browser, so these settings
          apply to this device only.
        </Text>
      </div>

      <Flex justify="space-between" align="center" gap="middle">
        <div>
          <Text strong>Remember column layouts</Text>
          <br />
          <Text type="secondary">
            Save each grid&apos;s column sizing, visibility, and pinning so
            your layout is restored on your next visit. Turning this off keeps
            existing saved layouts; they apply again when re-enabled.
          </Text>
        </div>
        <Switch
          checked={rememberColumnLayouts}
          onChange={setRememberColumnLayouts}
          aria-label="Remember column layouts"
        />
      </Flex>

      <Flex justify="space-between" align="center" gap="middle">
        <div>
          <Text strong>Reset all grid layouts</Text>
          <br />
          <Text type="secondary">
            Delete every saved column layout on this device. Grids return to
            their default columns.
          </Text>
        </div>
        <Popconfirm
          title="Reset all grid layouts?"
          description="This deletes every saved column layout on this device."
          okText="Reset"
          onConfirm={onResetAllGridLayouts}
        >
          <Button danger>Reset All</Button>
        </Popconfirm>
      </Flex>
    </Flex>
  )
}

export default Preferences
