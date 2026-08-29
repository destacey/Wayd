'use client'

import { LabeledContent } from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { RecordFactsGroup } from '@/src/components/common/record'
import { SprintDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex } from 'antd'
import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import Link from 'next/link'

dayjs.extend(utc)

/**
 * Sprint boundaries are UTC calendar dates. Formatting them in local time
 * shifts them a day for anyone behind UTC — the convention the sprints grid
 * documents.
 */
const formatDate = (value: Date) => dayjs.utc(value).format('MMM D, YYYY')

export interface SprintFactsProps {
  sprint: SprintDetailsDto
}

/**
 * A sprint's stable facts, for the details panel.
 *
 * Its dates and length; the team it belongs to sits under Relationships, as
 * the sprint's container rather than one of its attributes.
 */
const SprintFacts = ({ sprint }: SprintFactsProps) => {
  // Inclusive of both endpoints: a Mon-Fri sprint is five days, not four.
  const days = dayjs.utc(sprint.end).diff(dayjs.utc(sprint.start), 'day') + 1

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Start">
          {formatDate(sprint.start)}
        </LabeledContent>

        <LabeledContent label="End">{formatDate(sprint.end)}</LabeledContent>

        {days > 0 && (
          <LabeledContent label="Length">
            {days.toLocaleString()} day{days === 1 ? '' : 's'}
          </LabeledContent>
        )}
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Team">
          <Link href={`/organizations/teams/${sprint.team?.key}`}>
            {sprint.team?.name}
          </Link>
        </LabeledContent>
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <LinksCard objectId={sprint.id} width="100%" />
    </>
  )
}

export default SprintFacts
