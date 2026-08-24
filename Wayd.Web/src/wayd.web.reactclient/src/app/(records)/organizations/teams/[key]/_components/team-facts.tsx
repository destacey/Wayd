'use client'

import { LabeledContent } from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import {
  RecordFactsGroup,
  RecordLinkList,
} from '@/src/components/common/record'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import {
  SizingMethod,
  TeamDetailsDto,
  TeamOfTeamsDetailsDto,
} from '@/src/services/wayd-api'
import { useGetTeamOfTeamsMembershipsQuery } from '@/src/store/features/organizations/team-api'
import { teamUrl } from '@/src/utils'
import { Divider, Flex, Typography } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'

const { Text } = Typography

const getSizingMethodDisplayName = (sizingMethod: SizingMethod): string =>
  sizingMethod === SizingMethod.StoryPoints ? 'Story Points' : sizingMethod

export interface TeamFactsProps {
  /** Serves both team types — the shared facts are the same on each. */
  team: TeamDetailsDto | TeamOfTeamsDetailsDto
  /** Scrum/Kanban and sizing, which only a team (not a team of teams) has. */
  operatingModel?: TeamDetailsDto['operatingModel']
  /** True for a team of teams, which alone can have child teams. */
  hasChildTeams?: boolean
}

/**
 * A team's stable facts, for the details panel.
 *
 * Two groups: what the record is, then what it is connected to. No card of its
 * own — the panel supplies the frame, and at mobile widths the same stack
 * renders inline.
 */
const TeamFacts = ({
  team,
  operatingModel,
  hasChildTeams = false,
}: TeamFactsProps) => {
  const { data: memberships } = useGetTeamOfTeamsMembershipsQuery(
    { teamId: team.id },
    { skip: !hasChildTeams || !team.id },
  )

  // Memberships run both ways — rows where this record is the child carry its
  // own parent. Keep only the ones where it is the parent, and only those
  // currently in effect, so a closed membership does not read as a live team.
  const childTeams = (memberships ?? [])
    .filter((m) => m.parent.id === team.id && !m.end)
    .map((m) => m.child)
    .sort((a, b) => caseInsensitiveCompare(a.name, b.name))

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Type">{team.type}</LabeledContent>

        {/* The chip above shows the code, so the numeric key belongs here — it
            is what the route resolves on and what an API caller needs. */}
        <LabeledContent label="Key">{team.key}</LabeledContent>

        {operatingModel && (
          <>
            <LabeledContent label="Methodology">
              {operatingModel.methodology}
            </LabeledContent>
            <LabeledContent label="Sizing Method">
              {getSizingMethodDisplayName(operatingModel.sizingMethod)}
            </LabeledContent>
          </>
        )}

        <LabeledContent label="Active">
          {dayjs(team.activeDate).format('MMM D, YYYY')}
        </LabeledContent>

        {team.isActive === false && (
          <LabeledContent label="Inactive">
            {dayjs(team.inactiveDate).format('MMM D, YYYY')}
          </LabeledContent>
        )}

        {team.description && (
          <LabeledContent label="Description">
            <MarkdownRenderer markdown={team.description} />
          </LabeledContent>
        )}
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Parent Team">
          {team.teamOfTeams ? (
            <Link href={`/organizations/team-of-teams/${team.teamOfTeams.key}`}>
              {team.teamOfTeams.name}
            </Link>
          ) : (
            <Text type="secondary">None</Text>
          )}
        </LabeledContent>

        {childTeams.length > 0 && (
          <LabeledContent label="Teams">
            <RecordLinkList
              items={childTeams.map((child) => ({
                id: child.id,
                name: child.name,
                // teamUrl branches on type, so a nested team-of-teams links to
                // its own route rather than the teams one.
                href: teamUrl(child),
              }))}
            />
          </LabeledContent>
        )}
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <LinksCard objectId={team.id} width="100%" />
    </>
  )
}

export default TeamFacts
