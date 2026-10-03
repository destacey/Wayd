'use client'

import { LabeledContent } from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { PlanningIntervalDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex } from 'antd'
import { calendarDaysBetween, formatCalendarDate } from '@/src/utils'

export interface PlanningIntervalFactsProps {
  planningInterval: PlanningIntervalDetailsDto
}

/**
 * A planning interval's stable facts, for the details panel.
 *
 * Predictability sits here rather than only on Plan Review, where it was a tag
 * on one section of the record it describes.
 */
const PlanningIntervalFacts = ({
  planningInterval,
}: PlanningIntervalFactsProps) => {
  // Inclusive of both endpoints: a PI running the 5th to the 18th is 14 days.
  const days =
    calendarDaysBetween(planningInterval.start, planningInterval.end) + 1

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Start">
          {formatCalendarDate(planningInterval.start)}
        </LabeledContent>

        <LabeledContent label="End">
          {formatCalendarDate(planningInterval.end)}
        </LabeledContent>

        {days > 0 && (
          <LabeledContent label="Length">
            {days.toLocaleString()} day{days === 1 ? '' : 's'}
          </LabeledContent>
        )}

        {planningInterval.predictability != null && (
          <LabeledContent
            label="Predictability"
            tooltip="How much of what the teams committed to they delivered."
          >
            {planningInterval.predictability}%
          </LabeledContent>
        )}

        <LabeledContent label="Objectives Locked">
          {planningInterval.objectivesLocked ? 'Yes' : 'No'}
        </LabeledContent>

        {planningInterval.description && (
          <LabeledContent label="Description">
            <MarkdownRenderer markdown={planningInterval.description} />
          </LabeledContent>
        )}
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      <LinksCard objectId={planningInterval.id} width="100%" />
    </>
  )
}

export default PlanningIntervalFacts
