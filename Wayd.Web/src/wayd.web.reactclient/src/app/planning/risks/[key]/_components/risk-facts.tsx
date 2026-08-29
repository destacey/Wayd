'use client'

import { LabeledContent } from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import {
  RecordFactsGroup,
  RecordPersonLink,
} from '@/src/components/common/record'
import { RiskDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex, Typography } from 'antd'
import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import Link from 'next/link'

dayjs.extend(utc)

const { Text } = Typography

/**
 * Risk dates are UTC calendar dates. Formatting them in local time shifts them
 * a day earlier for anyone behind UTC, so they are rendered via `dayjs.utc` —
 * the same convention the sprints grid documents.
 */
const formatDate = (value: Date) => dayjs.utc(value).format('MMM D, YYYY')

export interface RiskFactsProps {
  risk: RiskDetailsDto
}

/**
 * A risk's stable facts, for the details panel.
 *
 * A risk is a small flat record — its grading, ownership and dates are the
 * whole of it, so everything but the narrative lives here.
 */
/**
 * How long the risk has been open, or was open before it closed.
 *
 * A raw reported date makes the reader do this arithmetic, and "open 94 days"
 * is the part that says whether a risk is being sat on.
 */
const ageOf = (reportedOn: Date, closedDate?: Date) => {
  const end = closedDate ? dayjs.utc(closedDate) : dayjs.utc()
  const days = end.diff(dayjs.utc(reportedOn), 'day')
  if (days < 0) return null

  const span =
    days === 0 ? 'today' : days === 1 ? '1 day' : `${days.toLocaleString()} days`

  if (closedDate) {
    return days === 0 ? 'Closed same day' : `Open ${span}`
  }
  return days === 0 ? 'Opened today' : `Open ${span}`
}

/**
 * How long until the follow-up is due, or how far past it.
 *
 * Overdue is the state worth acting on, so it is the one the label names —
 * and it is coloured, since a date alone leaves the reader to work out that
 * it has passed.
 */
const followUpStatus = (followUpDate: Date, closedDate?: Date) => {
  // A closed risk is not chased, so its follow-up date is just history.
  if (closedDate) return null

  const days = dayjs.utc(followUpDate).diff(dayjs.utc().startOf('day'), 'day')

  if (days < 0) {
    const overdue = Math.abs(days)
    return {
      label: `Overdue by ${overdue.toLocaleString()} day${overdue === 1 ? '' : 's'}`,
      overdue: true,
    }
  }
  if (days === 0) return { label: 'Due today', overdue: false }
  return {
    label: `Due in ${days.toLocaleString()} day${days === 1 ? '' : 's'}`,
    overdue: false,
  }
}

const RiskFacts = ({ risk }: RiskFactsProps) => {
  const ageLabel = ageOf(risk.reportedOn, risk.closedDate)
  const followUp = risk.followUpDate
    ? followUpStatus(risk.followUpDate, risk.closedDate)
    : null

  // teamUrl takes a TeamNavigationDto; a risk carries the planning projection,
  // so the branch is repeated here rather than reaching for that helper.
  const teamHref =
    risk.team?.type === 'Team'
      ? `/organizations/teams/${risk.team.key}`
      : `/organizations/team-of-teams/${risk.team?.key}`

  return (
    <>
      <Flex vertical gap={10}>
        {/* Status is the identity bar's descriptor, the ROAM category heads the
            content, and impact and likelihood are the matrix's axes — so the
            panel carries only what is not already shown elsewhere. */}

        {/* Who owns a risk is not context to it, so assignee and reporter sit
            with the details rather than under Relationships. Each is paired
            with its own date: who is chasing it and by when, then who raised
            it and how long ago. */}
        <LabeledContent label="Assignee">
          {risk.assignee ? (
            <RecordPersonLink
              name={risk.assignee.name}
              href={`/organizations/employees/${risk.assignee.key}`}
            />
          ) : (
            <Text type="secondary">Unassigned</Text>
          )}
        </LabeledContent>

        {risk.followUpDate && (
          <LabeledContent label="Follow-Up Date">
            <Flex vertical>
              {formatDate(risk.followUpDate)}
              {followUp && (
                <Text
                  type={followUp.overdue ? 'danger' : 'secondary'}
                  style={{ fontSize: 12 }}
                >
                  {followUp.label}
                </Text>
              )}
            </Flex>
          </LabeledContent>
        )}

        <LabeledContent label="Reported By">
          <RecordPersonLink
            name={risk.reportedBy.name}
            href={`/organizations/employees/${risk.reportedBy.key}`}
          />
        </LabeledContent>

        <LabeledContent label="Reported On">
          <Flex vertical>
            {formatDate(risk.reportedOn)}
            {ageLabel && (
              <Text type="secondary" style={{ fontSize: 12 }}>
                {ageLabel}
              </Text>
            )}
          </Flex>
        </LabeledContent>

        {risk.closedDate && (
          <LabeledContent label="Closed">
            {formatDate(risk.closedDate)}
          </LabeledContent>
        )}
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      {/* The team is the risk's container, not one of its attributes — the
          same role the identity bar's parent link plays. */}
      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Team">
          {risk.team ? (
            <Link href={teamHref}>{risk.team.name}</Link>
          ) : (
            <Text type="secondary">None</Text>
          )}
        </LabeledContent>
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <LinksCard objectId={risk.id} width="100%" />
    </>
  )
}

export default RiskFacts
