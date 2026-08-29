'use client'

import { LabeledContent } from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { RecordFactsGroup } from '@/src/components/common/record'
import { PlanningIntervalObjectiveDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex } from 'antd'
import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import Link from 'next/link'

dayjs.extend(utc)

/**
 * Objective dates are UTC calendar dates. Formatting them in local time shifts
 * them a day for anyone behind UTC — the convention the sprints grid documents.
 */
const formatDate = (value: Date) => dayjs.utc(value).format('MMM D, YYYY')

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
            {formatDate(objective.startDate)}
          </LabeledContent>
        )}

        {objective.targetDate && (
          <LabeledContent label="Target">
            {formatDate(objective.targetDate)}
          </LabeledContent>
        )}

        {objective.closedDate && (
          <LabeledContent label="Closed">
            {formatDate(objective.closedDate)}
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
