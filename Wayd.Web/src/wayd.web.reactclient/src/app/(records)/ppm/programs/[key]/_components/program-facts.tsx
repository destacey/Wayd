'use client'

import { WaydDateRange } from '@/src/components/common'
import {
  ContentList,
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { RecordFactsGroup } from '@/src/components/common/record'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { ProgramDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex } from 'antd'
import Link from 'next/link'
import RecordRoleList from '../../../_components/record-role-list'

export interface ProgramFactsProps {
  program: ProgramDetailsDto
}

/**
 * A program's stable facts, for the details panel.
 *
 * Its dates, themes and description; then the people accountable for it, and
 * last the portfolio it belongs to.
 */
const ProgramFacts = ({ program }: ProgramFactsProps) => {
  const strategicThemeNames = [...program.strategicThemes]
    .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
    .map((t) => t.name)

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Dates">
          <WaydDateRange
            dateRange={{ start: program.start, end: program.end }}
          />
        </LabeledContent>

        {strategicThemeNames.length > 0 && (
          <LabeledContent label="Strategic Themes">
            <ContentList items={strategicThemeNames} />
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

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Roles">
        <LabeledContent label="Sponsors">
          <RecordRoleList
            people={program.programSponsors}
            emptyText="No sponsor assigned"
          />
        </LabeledContent>

        <LabeledContent label="Owners">
          <RecordRoleList
            people={program.programOwners}
            emptyText="No owner assigned"
          />
        </LabeledContent>

        <LabeledContent label="PMs" tooltip="Program Managers">
          <RecordRoleList
            people={program.programManagers}
            emptyText="No PM assigned"
          />
        </LabeledContent>
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Portfolio">
          <Link href={`/ppm/portfolios/${program.portfolio.key}`}>
            {program.portfolio.name}
          </Link>
        </LabeledContent>
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <LinksCard objectId={program.id} width="100%" />
    </>
  )
}

export default ProgramFacts
