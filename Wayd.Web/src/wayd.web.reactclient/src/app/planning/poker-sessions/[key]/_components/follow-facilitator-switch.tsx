'use client'

import { Flex, Switch, Tooltip, Typography } from 'antd'
import { FC } from 'react'

const { Text } = Typography

export interface FollowFacilitatorSwitchProps {
  following: boolean
  onChange: (following: boolean) => void
}

/**
 * Whether this screen shows the round the facilitator has open. It is the
 * viewer's own choice, kept for this visit only.
 */
const FollowFacilitatorSwitch: FC<FollowFacilitatorSwitchProps> = ({
  following,
  onChange,
}) => (
  <Tooltip title="Shows the round the facilitator has open. Selecting a round yourself turns this off.">
    <Flex align="center" gap={8}>
      <Switch
        size="small"
        checked={following}
        onChange={onChange}
        aria-label="Follow facilitator"
      />
      <Text>Follow facilitator</Text>
    </Flex>
  </Tooltip>
)

export default FollowFacilitatorSwitch
