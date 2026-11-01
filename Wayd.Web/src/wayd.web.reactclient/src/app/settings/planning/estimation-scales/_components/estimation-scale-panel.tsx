'use client'

import { LabeledContent } from '@/src/components/common/content'
import {
  EstimationScaleDto,
  EstimationScaleValueDto,
  EstimationScaleVersionDto,
} from '@/src/services/wayd-api'
import { Flex, Space, Tag, Typography } from 'antd'
import dayjs from 'dayjs'

const { Text } = Typography

export interface EstimationScalePanelProps {
  estimationScale: EstimationScaleDto | undefined
  /** Every published version, newest first. Omitted while it loads. */
  versions?: EstimationScaleVersionDto[]
}

const ValueTags = ({ values }: { values: EstimationScaleValueDto[] }) => (
  <Space size={[4, 4]} wrap>
    {values.map((value) => (
      <Tag key={value.value}>{value.value}</Tag>
    ))}
  </Space>
)

/**
 * An estimation scale's fields, for the config list's detail panel.
 *
 * The values are tags rather than a list: a scale's values are a single
 * bounded fact about it — the sequence you estimate in — not a child
 * collection, so they wrap inside the panel rather than earning a section.
 * Estimates and cards are labelled apart, since only an estimate can become
 * a size.
 */
const EstimationScalePanel = ({
  estimationScale,
  versions,
}: EstimationScalePanelProps) => {
  if (!estimationScale) return null

  const current = estimationScale.currentVersion
  const estimates = current.values.filter((v) => v.isEstimate)
  const cards = current.values.filter((v) => !v.isEstimate)
  const history = (versions ?? []).filter((v) => v.id !== current.id)

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
      <LabeledContent label="Version">{current.number}</LabeledContent>
      <LabeledContent label="Estimates">
        <ValueTags values={estimates} />
      </LabeledContent>
      {cards.length > 0 && (
        <LabeledContent label="Cards">
          <ValueTags values={cards} />
        </LabeledContent>
      )}
      {history.length > 0 && (
        <LabeledContent label="Earlier Versions">
          <Flex vertical gap={6}>
            {history.map((version) => (
              <Flex key={version.id} vertical gap={2}>
                <Text type="secondary">
                  {`Version ${version.number} · ${dayjs(version.publishedOn).format('MMM D, YYYY')}`}
                </Text>
                <ValueTags values={version.values} />
              </Flex>
            ))}
          </Flex>
        </LabeledContent>
      )}
    </Flex>
  )
}

export default EstimationScalePanel
