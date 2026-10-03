'use client'

import { LabeledContent } from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { RecordFactsGroup } from '@/src/components/common/record'
import { PlanningIntervalObjectiveDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex } from 'antd'
import { formatCalendarDate } from '@/src/utils'
import dayjs from 'dayjs'
import Link from 'next/link'

export interface PlanningIntervalObjectiveFactsProps {
  objective: PlanningIntervalObjectiveDetailsDto
}

/**
 * A PI objective's stable facts, for the details panel.
 *
 * The PI and the team own the objective rather than describing it, so both sit
 * under Relationships.
 */
const PlanningIntervalObjectiveFacts = ({
  objective,
}: PlanningIntervalObjectiveFactsProps) => {
  const teamLink =
    objective.team?.type === 'Team'
      ? `/organizations/teams/${objective.team?.key}`
      : `/organizations/team-of-teams/${objective.team?.key}`

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Status">{objective.status?.name}</LabeledContent>

        <LabeledContent label="Type">{objective.type?.name}</LabeledContent>

        <LabeledContent
          label="Stretch"
          tooltip="A stretch objective is planned but not committed to."
        >
          {objective.isStretch ? 'Yes' : 'No'}
        </LabeledContent>

        {objective.startDate && (
          <LabeledContent label="Start">
            {formatCalendarDate(objective.startDate)}
          </LabeledContent>
        )}

        {objective.targetDate && (
          <LabeledContent label="Target">
            {formatCalendarDate(objective.targetDate)}
          </LabeledContent>
        )}

        {objective.closedDate && (
          <LabeledContent label="Closed">
            {dayjs(objective.closedDate).format('MMM D, YYYY')}
          </LabeledContent>
        )}

        {objective.description && (
          <LabeledContent label="Description">
            <MarkdownRenderer markdown={objective.description} />
          </LabeledContent>
        )}
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Planning Interval">
          <Link
            href={`/planning/planning-intervals/${objective.planningInterval?.key}`}
          >
            {objective.planningInterval?.name}
          </Link>
        </LabeledContent>

        <LabeledContent label="Team">
          <Link href={teamLink}>{objective.team?.name}</Link>
        </LabeledContent>
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <LinksCard objectId={objective.id} width="100%" />
    </>
  )
}

export default PlanningIntervalObjectiveFacts
