'use client'

import { WaydDateRange } from '@/src/components/common'
import { ContentList, LabeledContent } from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import { ProjectDetailsDto } from '@/src/services/wayd-api'
import { Card, Divider, Flex } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import dayjs from 'dayjs'
import Link from 'next/link'
import { FC } from 'react'
import ProjectScoreCard from '../[key]/_components/scoring/project-score-card'

export interface ProjectDetailsProps {
  project: ProjectDetailsDto
}

const ProjectDetails: FC<ProjectDetailsProps> = ({ project }) => {
  if (!project) return null

  const sponsorNames = [...project.projectSponsors]
    .sort((a, b) => a.name.localeCompare(b.name))
    .map((s) => s.name)

  const ownerNames = [...project.projectOwners]
    .sort((a, b) => a.name.localeCompare(b.name))
    .map((o) => o.name)

  const managerNames = [...project.projectManagers]
    .sort((a, b) => a.name.localeCompare(b.name))
    .map((m) => m.name)

  const memberNames = [...project.projectMembers]
    .sort((a, b) => a.name.localeCompare(b.name))
    .map((m) => m.name)

  const strategicThemeNames = [...project.strategicThemes]
    .sort((a, b) => a.name.localeCompare(b.name))
    .map((t) => t.name)

  const sortedStrategicInitiatives = [...project.strategicInitiatives].sort(
    (a, b) => a.name.localeCompare(b.name),
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
    <Card size="small">
      <Flex vertical gap={0}>
        <Flex vertical gap={10}>
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

          {sortedStrategicInitiatives.length > 0 && (
            <LabeledContent label="Strategic Initiatives">
              <ul style={{ margin: 0, paddingLeft: 16 }}>
                {sortedStrategicInitiatives.map((si) => (
                  <li key={si.id}>
                    <Link href={`/ppm/strategic-initiatives/${si.key}`}>
                      {si.name}
                    </Link>
                  </li>
                ))}
              </ul>
            </LabeledContent>
          )}

          <Divider size="small" />

          <LabeledContent label="Sponsors">
            <ContentList items={sponsorNames} emptyText="No sponsor assigned" />
          </LabeledContent>

          <LabeledContent label="Owners">
            <ContentList items={ownerNames} emptyText="No owner assigned" />
          </LabeledContent>

          <LabeledContent label="PMs" tooltip="Project Managers">
            <ContentList items={managerNames} emptyText="No PM assigned" />
          </LabeledContent>

          <LabeledContent label="Members">
            <ContentList items={memberNames} emptyText="No members assigned" />
          </LabeledContent>
        </Flex>

        {hasStarted && (
          <>
            <Divider />
            <TimelineProgress
              start={project.start ?? null}
              end={project.end ?? null}
              variant="borderless"
              style={{ width: '100%' }}
              dateFormat={timelineFormat}
            />
          </>
        )}

        <ProjectScoreCard
          projectId={project.id}
          scoringModel={project.portfolioScoringModel}
          currentScore={project.currentScore}
          canManageProject={project.canManageProject}
          variant="section"
        />

        <Divider />

        <div style={{ padding: '0 16px 16px' }}>
          <LinksCard objectId={project.id} width="100%" />
        </div>
      </Flex>
    </Card>
  )
}

export default ProjectDetails

