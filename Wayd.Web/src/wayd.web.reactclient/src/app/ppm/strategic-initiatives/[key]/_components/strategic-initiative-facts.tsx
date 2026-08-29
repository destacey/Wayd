'use client'

import { WaydDateRange } from '@/src/components/common'
import {
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import { RecordFactsGroup } from '@/src/components/common/record'
import { StrategicInitiativeDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'
import RecordRoleList from '../../../_components/record-role-list'

export interface StrategicInitiativeFactsProps {
  strategicInitiative: StrategicInitiativeDetailsDto
}

/**
 * A strategic initiative's stable facts, for the details panel.
 *
 * Its dates and description, then the people accountable for it and the
 * portfolio it belongs to. Its progress through the date range closes the
 * panel, being a reading taken of the record rather than an attribute of it.
 */
const StrategicInitiativeFacts = ({
  strategicInitiative,
}: StrategicInitiativeFactsProps) => {
  const hasStarted =
    strategicInitiative.start &&
    dayjs(strategicInitiative.start).isBefore(dayjs(), 'day')

  const timelineFormat =
    strategicInitiative.start &&
    strategicInitiative.end &&
    new Date(strategicInitiative.start).getFullYear() ===
      new Date().getFullYear()
      ? 'MMM D'
      : 'MMM D, YYYY'

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Dates">
          <WaydDateRange
            dateRange={{
              start: strategicInitiative.start,
              end: strategicInitiative.end,
            }}
          />
        </LabeledContent>

        {strategicInitiative.description && (
          <LabeledContent label="Description">
            <ExpandableContent>
              <MarkdownRenderer markdown={strategicInitiative.description} />
            </ExpandableContent>
          </LabeledContent>
        )}
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Roles">
        <LabeledContent label="Sponsors">
          <RecordRoleList
            people={strategicInitiative.strategicInitiativeSponsors}
            emptyText="No sponsor assigned"
          />
        </LabeledContent>

        <LabeledContent label="Owners">
          <RecordRoleList
            people={strategicInitiative.strategicInitiativeOwners}
            emptyText="No owner assigned"
          />
        </LabeledContent>
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Portfolio">
          <Link href={`/ppm/portfolios/${strategicInitiative.portfolio.key}`}>
            {strategicInitiative.portfolio.name}
          </Link>
        </LabeledContent>
      </RecordFactsGroup>

      {hasStarted && (
        <>
          <Divider size="small" style={{ margin: 0 }} />
          <TimelineProgress
            start={strategicInitiative.start ?? null}
            end={strategicInitiative.end ?? null}
            variant="borderless"
            style={{ width: '100%' }}
            dateFormat={timelineFormat}
          />
        </>
      )}

      <Divider size="small" style={{ margin: 0 }} />

      <LinksCard objectId={strategicInitiative.id} width="100%" />
    </>
  )
}

export default StrategicInitiativeFacts
