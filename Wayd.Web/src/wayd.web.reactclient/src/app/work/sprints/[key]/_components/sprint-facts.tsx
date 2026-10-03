'use client'

import { LabeledContent } from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { RecordFactsGroup } from '@/src/components/common/record'
import { SprintDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex, Tag, Typography } from 'antd'
import { calendarDaysBetween, formatCalendarDate } from '@/src/utils'
import dayjs from 'dayjs'
import Link from 'next/link'

const { Text } = Typography

const INSTANT_FORMAT = 'MMM D, YYYY h:mm A'

interface ActualMomentProps {
  value: Date
  recorded: boolean
}

// An effective moment is either what the team recorded or the default the
// planned dates imply; the tag says which, since both read as a time.
const ActualMoment = ({ value, recorded }: ActualMomentProps) => (
  <Flex gap={6} align="center" wrap>
    {dayjs(value).format(INSTANT_FORMAT)}
    <Tag bordered={false} color={recorded ? 'processing' : 'default'}>
      {recorded ? 'Actual' : 'Default'}
    </Tag>
  </Flex>
)

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
  const days = calendarDaysBetween(sprint.start, sprint.end) + 1

  // Times are shown in the viewer's zone; the defaults are computed in the
  // team's, so name it when the two differ.
  const viewerTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone
  const showTeamTimeZone =
    !!sprint.timeZone && sprint.timeZone !== viewerTimeZone

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Start">
          {formatCalendarDate(sprint.start)}
        </LabeledContent>

        <LabeledContent label="End">
          {formatCalendarDate(sprint.end)}
        </LabeledContent>

        {days > 0 && (
          <LabeledContent label="Length">
            {days.toLocaleString()} day{days === 1 ? '' : 's'}
          </LabeledContent>
        )}

        {sprint.effectiveStart && (
          <LabeledContent label="Actual start">
            <ActualMoment
              value={sprint.effectiveStart}
              recorded={!!sprint.started}
            />
          </LabeledContent>
        )}

        {sprint.effectiveStart && sprint.effectiveEnd && (
          <LabeledContent label="Actual end">
            <ActualMoment
              value={sprint.effectiveEnd}
              recorded={!!sprint.completed}
            />
          </LabeledContent>
        )}

        {sprint.effectiveStart && showTeamTimeZone && (
          <Text type="secondary">Team time zone: {sprint.timeZone}</Text>
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
