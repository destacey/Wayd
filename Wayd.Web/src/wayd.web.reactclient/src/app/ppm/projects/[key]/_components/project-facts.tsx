'use client'

import { WaydDateRange, WaydTooltip } from '@/src/components/common'
import { ContentList, LabeledContent } from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import {
  RecordFactsGroup,
  RecordLinkList,
} from '@/src/components/common/record'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { ProjectDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'
import ProjectScoreCard from '@/src/app/ppm/projects/_components/scoring/project-score-card'
import RecordRoleList from '../../../_components/record-role-list'

export interface ProjectFactsProps {
  project: ProjectDetailsDto
}

/**
 * A project's stable facts, for the details panel.
 *
 * What the project is — its dates, category and lifecycle — then the people
 * accountable for it, then what it sits under: the portfolio and program above
 * it and the initiatives it serves. Its progress and priority score close the
 * panel, being readings taken of the record rather than attributes of it.
 */
const ProjectFacts = ({ project }: ProjectFactsProps) => {
  const strategicThemeNames = [...project.strategicThemes]
    .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
    .map((t) => t.name)

  const sortedStrategicInitiatives = [...project.strategicInitiatives].sort(
    (a, b) => caseInsensitiveCompare(a.name, b.name),
  )

  const hasStarted =
    project.start && dayjs(project.start).isBefore(dayjs(), 'day')

  const timelineFormat =
    project.start &&
    project.end &&
    new Date(project.start).getFullYear() === new Date().getFullYear()
      ? 'MMM D'
      : 'MMM D, YYYY'

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Dates">
          <WaydDateRange
            dateRange={{ start: project.start, end: project.end }}
          />
        </LabeledContent>

        <LabeledContent label="Expenditure Category">
          {project.expenditureCategory.name}
        </LabeledContent>

        <LabeledContent label="Lifecycle">
          {project.projectLifecycle ? (
            <WaydTooltip title={project.projectLifecycle.description}>
              {project.projectLifecycle.name}
            </WaydTooltip>
          ) : (
            'No lifecycle assigned'
          )}
        </LabeledContent>

        {strategicThemeNames.length > 0 && (
          <LabeledContent label="Strategic Themes">
            <ContentList items={strategicThemeNames} />
          </LabeledContent>
        )}
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Roles">
        <LabeledContent label="Sponsors">
          <RecordRoleList
            people={project.projectSponsors}
            emptyText="No sponsor assigned"
          />
        </LabeledContent>

        <LabeledContent label="Owners">
          <RecordRoleList
            people={project.projectOwners}
            emptyText="No owner assigned"
          />
        </LabeledContent>

        <LabeledContent label="PMs" tooltip="Project Managers">
          <RecordRoleList
            people={project.projectManagers}
            emptyText="No PM assigned"
          />
        </LabeledContent>

        <LabeledContent label="Members">
          <RecordRoleList
            people={project.projectMembers}
            emptyText="No members assigned"
          />
        </LabeledContent>
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Portfolio">
          <Link href={`/ppm/portfolios/${project.portfolio.key}`}>
            {project.portfolio.name}
          </Link>
        </LabeledContent>

        {project.program && (
          <LabeledContent label="Program">
            <Link href={`/ppm/programs/${project.program.key}`}>
              {project.program.name}
            </Link>
          </LabeledContent>
        )}

        {sortedStrategicInitiatives.length > 0 && (
          <LabeledContent label="Strategic Initiatives">
            <RecordLinkList
              items={sortedStrategicInitiatives.map((si) => ({
                id: si.id,
                name: si.name,
                href: `/ppm/strategic-initiatives/${si.key}`,
              }))}
            />
          </LabeledContent>
        )}
      </RecordFactsGroup>

      {hasStarted && (
        <>
          <Divider size="small" style={{ margin: 0 }} />
          <TimelineProgress
            start={project.start ?? null}
            end={project.end ?? null}
            variant="borderless"
            style={{ width: '100%' }}
            dateFormat={timelineFormat}
          />
        </>
      )}

      {/* Renders nothing when the portfolio has no scoring model, so no
          divider of its own — it supplies one when it has something to show. */}
      <ProjectScoreCard
        projectId={project.id}
        scoringModel={project.portfolioScoringModel}
        currentScore={project.currentScore}
        canManageProject={project.canManageProject}
        variant="section"
      />

      <Divider size="small" style={{ margin: 0 }} />

      <LinksCard objectId={project.id} width="100%" />
    </>
  )
}

export default ProjectFacts
