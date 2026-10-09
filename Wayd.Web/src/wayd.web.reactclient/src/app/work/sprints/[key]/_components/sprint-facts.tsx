'use client'

import { LabeledContent } from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { RecordFactsGroup } from '@/src/components/common/record'
import { SprintDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex } from 'antd'
import {
  calendarDaysBetween,
  formatCalendarDate,
  sprintActiveDays,
} from '@/src/utils'
import dayjs from 'dayjs'
import Link from 'next/link'

const INSTANT_FORMAT = 'MMM D, YYYY h:mm A'

// Only what the team recorded, as the time it entered in the viewer's zone.
const formatActual = (recorded: Date | undefined) =>
  recorded ? dayjs(recorded).format(INSTANT_FORMAT) : '—'

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
  // The days the sprint is Active — its planned days, moved by any recorded
  // start or end — counted inclusively, so Mon-Fri is five days.
  const activeDays = sprintActiveDays(sprint)
  const days = calendarDaysBetween(activeDays.start, activeDays.end) + 1

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Planned start">
          {formatCalendarDate(sprint.start)}
        </LabeledContent>

        <LabeledContent label="Planned end">
          {formatCalendarDate(sprint.end)}
        </LabeledContent>

        {/* Only a sprint of a mapped team can have actual dates recorded. */}
        {sprint.activeFrom && (
          <>
            <LabeledContent label="Actual start">
              {formatActual(sprint.started)}
            </LabeledContent>

            <LabeledContent label="Actual end">
              {formatActual(sprint.completed)}
            </LabeledContent>
          </>
        )}

        {days > 0 && (
          <LabeledContent label="Length">
            {days.toLocaleString()} day{days === 1 ? '' : 's'}
          </LabeledContent>
        )}

        {sprint.teamDaysOff.length > 0 && (
          <LabeledContent label="Team days off">
            {sprint.teamDaysOff
              .map((d) => formatCalendarDate(d, 'ddd, MMM D'))
              .join('; ')}
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
