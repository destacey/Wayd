'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { PlanningIntervalIterationDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex } from 'antd'
import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import Link from 'next/link'

dayjs.extend(utc)

/**
 * Iteration boundaries are UTC calendar dates. Formatting them in local time
 * shifts them a day for anyone behind UTC — the convention the sprints grid
 * documents.
 */
const formatDate = (value: Date) => dayjs.utc(value).format('MMM D, YYYY')

export interface PlanningIntervalIterationFactsProps {
  iteration: PlanningIntervalIterationDetailsDto
}

/**
 * A PI iteration's stable facts, for the details panel.
 *
 * Its dates and length; the PI it belongs to sits under Relationships, as the
 * iteration's container rather than one of its attributes.
 */
const PlanningIntervalIterationFacts = ({
  iteration,
}: PlanningIntervalIterationFactsProps) => {
  // Inclusive of both endpoints: a Mon-Fri iteration is five days, not four.
  const days =
    dayjs.utc(iteration.end).diff(dayjs.utc(iteration.start), 'day') + 1

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Start">
          {formatDate(iteration.start)}
        </LabeledContent>

        <LabeledContent label="End">{formatDate(iteration.end)}</LabeledContent>

        {days > 0 && (
          <LabeledContent label="Length">
            {days.toLocaleString()} day{days === 1 ? '' : 's'}
          </LabeledContent>
        )}

        <LabeledContent label="Category">
          {iteration.category?.name}
        </LabeledContent>
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Planning Interval">
          <Link
            href={`/planning/planning-intervals/${iteration.planningInterval?.key}`}
          >
            {iteration.planningInterval?.name}
          </Link>
        </LabeledContent>
      </RecordFactsGroup>
    </>
  )
}

export default PlanningIntervalIterationFacts
