'use client'

import { WaydDateRange } from '@/src/components/common'
import {
  ContentList,
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import {
  RecordFactsGroup,
  RecordLinkList,
} from '@/src/components/common/record'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import StageTimeline from './stage-timeline'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { useGetProjectQuery } from '@/src/store/features/ppm/projects-api'
import { getDrawerWidthPixels, isApiError } from '@/src/utils'
import { Divider, Drawer, Flex } from 'antd'
import dayjs from 'dayjs'
import { WaydTooltip } from '@/src/components/common'
import { projectHelpText } from '../projects/_components/project-help-text'
import ProjectHealthCheckTag from '../projects/_components/project-health-check-tag'
import ProjectScoreCard from '../projects/_components/scoring/project-score-card'
import RecordRoleList from '@/src/app/ppm/_components/record-role-list'
import Link from 'next/link'
import { FC, useEffect, useState } from 'react'

export interface ProjectDrawerProps {
  projectKey: string
  drawerOpen: boolean
  onDrawerClose: () => void
}

const ProjectDrawer: FC<ProjectDrawerProps> = ({
  projectKey,
  drawerOpen,
  onDrawerClose,
}: ProjectDrawerProps) => {
  const [size, setSize] = useState(() => getDrawerWidthPixels())
  const messageApi = useMessage()

  const { data: projectData, isLoading, error } = useGetProjectQuery(projectKey)

  const { hasPermissionClaim } = useAuth()
  const canViewProject = hasPermissionClaim('Permissions.Projects.View')

  useEffect(() => {
    if (!canViewProject) {
      messageApi.error('You do not have permission to view projects.')
      onDrawerClose()
    }
  }, [canViewProject, messageApi, onDrawerClose])

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading project data. Please try again.',
      )
    }
  }, [error, messageApi])

  const strategicThemeNames = [...(projectData?.strategicThemes ?? [])]
    .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
    .map((t) => t.name)

  const sortedStrategicInitiatives = [
    ...(projectData?.strategicInitiatives ?? []),
  ].sort((a, b) => caseInsensitiveCompare(a.name, b.name))

  const hasStarted =
    projectData?.start && dayjs(projectData.start).isBefore(dayjs(), 'day')

  const hasNarrative = !!(
    projectData?.description ||
    projectData?.businessCase ||
    projectData?.expectedBenefits
  )

  const timelineFormat =
    projectData?.start &&
    projectData.end &&
    new Date(projectData.start).getFullYear() === new Date().getFullYear()
      ? 'MMM D'
      : 'MMM D, YYYY'

  return (
    <Drawer
      title={projectData?.name ?? 'Project Details'}
      placement="right"
      onClose={onDrawerClose}
      open={drawerOpen}
      loading={isLoading}
      size={size}
      resizable={{
        onResize: (newSize) => setSize(newSize),
      }}
      destroyOnHidden={true}
    >
      <Flex vertical gap="middle">
        <Flex vertical gap={10}>
          <LabeledContent label="Key">
            <Link href={`/ppm/projects/${projectData?.key}`}>
              {projectData?.key}
            </Link>
          </LabeledContent>
          <LabeledContent label="Status">
            {projectData?.status.name}
          </LabeledContent>
          {projectData?.healthCheck && (
            <LabeledContent label="Health">
              <ProjectHealthCheckTag
                healthCheck={projectData.healthCheck}
                projectId={projectData.id}
              />
            </LabeledContent>
          )}
          <LabeledContent label="Dates">
            <WaydDateRange
              dateRange={{ start: projectData?.start, end: projectData?.end }}
            />
          </LabeledContent>
          <LabeledContent label="Expenditure Category">
            {projectData?.expenditureCategory.name}
          </LabeledContent>
          <LabeledContent label="Lifecycle">
            {projectData?.projectLifecycle ? (
              <WaydTooltip title={projectData.projectLifecycle.description}>
                {projectData.projectLifecycle.name}
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
              people={projectData?.projectSponsors ?? []}
              emptyText="No sponsor assigned"
            />
          </LabeledContent>
          <LabeledContent label="Owners">
            <RecordRoleList
              people={projectData?.projectOwners ?? []}
              emptyText="No owner assigned"
            />
          </LabeledContent>
          <LabeledContent label="PMs" tooltip="Project Managers">
            <RecordRoleList
              people={projectData?.projectManagers ?? []}
              emptyText="No PM assigned"
            />
          </LabeledContent>
          <LabeledContent label="Members">
            <RecordRoleList
              people={projectData?.projectMembers ?? []}
              emptyText="No members assigned"
            />
          </LabeledContent>
        </RecordFactsGroup>

        <Divider size="small" style={{ margin: 0 }} />

        <RecordFactsGroup label="Relationships">
          {projectData?.portfolio && (
            <LabeledContent label="Portfolio">
              <Link href={`/ppm/portfolios/${projectData.portfolio.key}`}>
                {projectData.portfolio.name}
              </Link>
            </LabeledContent>
          )}

          {projectData?.program && (
            <LabeledContent label="Program">
              <Link href={`/ppm/programs/${projectData.program.key}`}>
                {projectData.program.name}
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
              start={projectData?.start ?? null}
              end={projectData?.end ?? null}
              variant="borderless"
              style={{ width: '100%' }}
              dateFormat={timelineFormat}
            />
          </>
        )}

        {hasNarrative && (
          <Divider size="small" style={{ margin: 0 }} />
        )}

        <Flex vertical gap={10}>
          {projectData?.description && (
            <LabeledContent
              label="Description"
              tooltip={projectHelpText.description}
            >
              <ExpandableContent background="var(--ant-color-bg-elevated)">
                <MarkdownRenderer markdown={projectData.description} />
              </ExpandableContent>
            </LabeledContent>
          )}

          {projectData?.businessCase && (
            <LabeledContent
              label="Business Case"
              tooltip={projectHelpText.businessCase}
            >
              <ExpandableContent background="var(--ant-color-bg-elevated)">
                <MarkdownRenderer markdown={projectData.businessCase} />
              </ExpandableContent>
            </LabeledContent>
          )}

          {projectData?.expectedBenefits && (
            <LabeledContent
              label="Expected Benefits"
              tooltip={projectHelpText.expectedBenefits}
            >
              <ExpandableContent background="var(--ant-color-bg-elevated)">
                <MarkdownRenderer markdown={projectData.expectedBenefits} />
              </ExpandableContent>
            </LabeledContent>
          )}
        </Flex>

        {(projectData?.stages?.length ?? 0) > 0 && (
          <StageTimeline stages={projectData!.stages} />
        )}

        {projectData?.id && (
          <ProjectScoreCard
            projectId={projectData.id}
            scoringModel={projectData.portfolioScoringModel}
            currentScore={projectData.currentScore}
            canManageProject={projectData.canManageProject}
            variant="section"
          />
        )}

        {projectData?.id && (
          <>
            <Divider size="small" />
            <LinksCard objectId={projectData.id} width="100%" />
          </>
        )}
      </Flex>
    </Drawer>
  )
}

export default ProjectDrawer

