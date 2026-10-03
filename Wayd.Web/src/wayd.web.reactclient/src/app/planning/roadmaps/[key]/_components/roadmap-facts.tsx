'use client'

import { LabeledContent } from '@/src/components/common/content'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import {
  RecordFactsGroup,
  RecordPersonLink,
} from '@/src/components/common/record'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { RoadmapDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex, Typography } from 'antd'
import { formatCalendarDate } from '@/src/utils'

const { Text } = Typography

export interface RoadmapFactsProps {
  roadmap: RoadmapDetailsDto
}

/**
 * A roadmap's stable facts, for the details panel.
 *
 * Its span and visibility, then who manages it — which the legacy page carried
 * only inside a tooltip on the visibility icon, so the names were unreachable
 * without hovering and unlinkable once found.
 */
const RoadmapFacts = ({ roadmap }: RoadmapFactsProps) => {
  const managers = [...roadmap.roadmapManagers].sort((a, b) =>
    caseInsensitiveCompare(a.name, b.name),
  )

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Start">
          {formatCalendarDate(roadmap.start)}
        </LabeledContent>

        <LabeledContent label="End">
          {formatCalendarDate(roadmap.end)}
        </LabeledContent>

        <LabeledContent label="Visibility">
          {roadmap.visibility?.name}
        </LabeledContent>

        {roadmap.description && (
          <LabeledContent label="Description">
            <MarkdownRenderer markdown={roadmap.description} />
          </LabeledContent>
        )}
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Managers">
          {managers.length ? (
            <Flex vertical gap={6}>
              {managers.map((manager) => (
                <RecordPersonLink
                  key={manager.id}
                  name={manager.name}
                  href={`/organizations/employees/${manager.key}`}
                />
              ))}
            </Flex>
          ) : (
            <Text type="secondary">None assigned</Text>
          )}
        </LabeledContent>
      </RecordFactsGroup>
    </>
  )
}

export default RoadmapFacts
