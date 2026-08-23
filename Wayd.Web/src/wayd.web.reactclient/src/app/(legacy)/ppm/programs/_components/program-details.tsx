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
import { ProgramDetailsDto } from '@/src/services/wayd-api'
import { getSortedNameList } from '@/src/utils'
import { Card, Divider, Flex } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'
import { FC } from 'react'

export interface ProgramDetailsProps {
  program: ProgramDetailsDto
}

const ProgramDetails: FC<ProgramDetailsProps> = ({ program }) => {
  const sponsorNames = program ? getSortedNameList(program.programSponsors) : []
  const ownerNames = program ? getSortedNameList(program.programOwners) : []
  const managerNames = program ? getSortedNameList(program.programManagers) : []
  const strategicThemes = program
    ? getSortedNameList(program.strategicThemes)
    : []

  if (!program) return null

  const hasStarted =
    program.start && dayjs(program.start).isBefore(dayjs(), 'day')

  const timelineFormat =
    program.start &&
    program.end &&
    new Date(program.start).getFullYear() === new Date().getFullYear()
      ? 'MMM D'
      : 'MMM D, YYYY'

  return (
    <Card size="small">
      <Flex vertical gap={0}>
        <Flex vertical gap={10}>
          <LabeledContent label="Portfolio">
            <Link href={`/ppm/portfolios/${program.portfolio.key}`}>
              {program.portfolio.name}
            </Link>
          </LabeledContent>

          <LabeledContent label="Dates">
            <WaydDateRange
              dateRange={{ start: program.start, end: program.end }}
            />
          </LabeledContent>

          <LabeledContent label="Sponsors">
            <ContentList items={sponsorNames} emptyText="No sponsor assigned" />
          </LabeledContent>

          <LabeledContent label="Owners">
            <ContentList items={ownerNames} emptyText="No owner assigned" />
          </LabeledContent>

          <LabeledContent label="PMs" tooltip="Program Managers">
            <ContentList items={managerNames} emptyText="No PM assigned" />
          </LabeledContent>

          {strategicThemes.length > 0 && (
            <LabeledContent label="Strategic Themes">
              {strategicThemes.join(', ')}
            </LabeledContent>
          )}

          {program.description && (
            <LabeledContent label="Description">
              <ExpandableContent>
                <MarkdownRenderer markdown={program.description} />
              </ExpandableContent>
            </LabeledContent>
          )}
        </Flex>

        {hasStarted && (
          <>
            <Divider />
            <TimelineProgress
              start={program.start ?? null}
              end={program.end ?? null}
              variant="borderless"
              style={{ width: '100%' }}
              dateFormat={timelineFormat}
            />
          </>
        )}

        <Divider />

        <LinksCard objectId={program.id} width="100%" />
      </Flex>
    </Card>
  )
}

export default ProgramDetails
