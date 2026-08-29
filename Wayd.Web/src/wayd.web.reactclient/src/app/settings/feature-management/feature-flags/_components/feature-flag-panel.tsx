'use client'

import { LabeledContent } from '@/src/components/common/content'
import { FeatureFlagDto } from '@/src/services/wayd-api'
import { Flex, Tag } from 'antd'

export interface FeatureFlagPanelProps {
  featureFlag: FeatureFlagDto | undefined
}

/**
 * A feature flag's fields, for the config list's detail panel.
 *
 * `name` is the value the code gates on, so it stays even though the list
 * shows `displayName` — someone reading this panel is usually about to search
 * for that string.
 */
const FeatureFlagPanel = ({ featureFlag }: FeatureFlagPanelProps) => {
  if (!featureFlag) return null

  return (
    <Flex vertical gap={10}>
      <LabeledContent label="Name">{featureFlag.name}</LabeledContent>
      <LabeledContent label="Enabled">
        {featureFlag.isEnabled ? 'Yes' : 'No'}
      </LabeledContent>
      <LabeledContent label="Type">
        {featureFlag.isSystem ? 'System' : 'User'}
      </LabeledContent>
      {featureFlag.isArchived && (
        <LabeledContent label="Status">
          <Tag>Archived</Tag>
        </LabeledContent>
      )}
      {featureFlag.description && (
        <LabeledContent label="Description">
          {featureFlag.description}
        </LabeledContent>
      )}
    </Flex>
  )
}

export default FeatureFlagPanel
