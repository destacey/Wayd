'use client'

import { WaydDateRange } from '@/src/components/common'
import {
  ContentList,
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import { StrategicInitiativeDetailsDto } from '@/src/services/wayd-api'
import { Card, Divider, Flex } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'
import { FC } from 'react'

export interface StrategicInitiativeDetailsProps {
  strategicInitiative: StrategicInitiativeDetailsDto
}

const StrategicInitiativeDetails: FC<StrategicInitiativeDetailsProps> = ({
  strategicInitiative,
}) => {
  if (!strategicInitiative) return null

  const sponsorNames = [...strategicInitiative.strategicInitiativeSponsors]
    .sort((a, b) => a.name.localeCompare(b.name))
    .map((s) => s.name)

  const ownerNames = [...strategicInitiative.strategicInitiativeOwners]
    .sort((a, b) => a.name.localeCompare(b.name))
    .map((o) => o.name)

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
    <Card size="small">
      <Flex vertical gap={0}>
        <Flex vertical gap={10}>
          <LabeledContent label="Portfolio">
            <Link href={`/ppm/portfolios/${strategicInitiative.portfolio.key}`}>
              {strategicInitiative.portfolio.name}
            </Link>
          </LabeledContent>

          <LabeledContent label="Dates">
            <WaydDateRange
              dateRange={{
                start: strategicInitiative.start,
                end: strategicInitiative.end,
              }}
            />
          </LabeledContent>

          <LabeledContent label="Owners">
            <ContentList items={ownerNames} emptyText="No owner assigned" />
          </LabeledContent>

          <LabeledContent label="Sponsors">
            <ContentList items={sponsorNames} emptyText="No sponsor assigned" />
          </LabeledContent>

          {strategicInitiative.description && (
            <LabeledContent label="Description">
              <ExpandableContent>
                <MarkdownRenderer markdown={strategicInitiative.description} />
              </ExpandableContent>
            </LabeledContent>
          )}
        </Flex>

        {hasStarted && (
          <>
            <Divider />
            <TimelineProgress
              start={strategicInitiative.start ?? null}
              end={strategicInitiative.end ?? null}
              variant="borderless"
              style={{ width: '100%' }}
              dateFormat={timelineFormat}
            />
          </>
        )}

        <Divider />

        {/* Links — uses its own card styling internally, remove outer card */}
        <div style={{ padding: '0 16px 16px' }}>
          <LinksCard objectId={strategicInitiative.id} width="100%" />
        </div>
      </Flex>
    </Card>
  )
}

export default StrategicInitiativeDetails
