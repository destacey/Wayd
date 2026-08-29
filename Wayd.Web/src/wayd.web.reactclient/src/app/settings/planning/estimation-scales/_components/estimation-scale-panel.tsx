'use client'

import { LabeledContent } from '@/src/components/common/content'
import { EstimationScaleDto } from '@/src/services/wayd-api'
import { Flex, Space, Tag } from 'antd'

export interface EstimationScalePanelProps {
  estimationScale: EstimationScaleDto | undefined
}

/**
 * An estimation scale's fields, for the config list's detail panel.
 *
 * The values are tags rather than a list: a scale's values are a single
 * bounded fact about it — the sequence you estimate in — not a child
 * collection, so they wrap inside the panel rather than earning a section.
 */
const EstimationScalePanel = ({
  estimationScale,
}: EstimationScalePanelProps) => {
  if (!estimationScale) return null

  return (
    <Flex vertical gap={10}>
      <LabeledContent label="Active">
        {estimationScale.isActive ? 'Yes' : 'No'}
      </LabeledContent>
      {estimationScale.description && (
        <LabeledContent label="Description">
          {estimationScale.description}
        </LabeledContent>
      )}
      <LabeledContent label="Values">
        <Space size={[4, 4]} wrap>
          {estimationScale.values.map((value, index) => (
            <Tag key={index}>{value}</Tag>
          ))}
        </Space>
      </LabeledContent>
    </Flex>
  )
}

export default EstimationScalePanel
