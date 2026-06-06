'use client'

import { WaydDateRange } from '@/src/components/common'
import {
  ContentList,
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import PhaseTimeline from './phase-timeline'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { useGetProjectQuery } from '@/src/store/features/ppm/projects-api'
import { getDrawerWidthPixels, getSortedNameList, isApiError} from '@/src/utils'
import { Divider, Drawer, Flex } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import { projectHelpText } from '../projects/_components/project-help-text'
import ProjectHealthCheckTag from '../projects/_components/project-health-check-tag'
import ProjectScoreCard from '../projects/[key]/_components/scoring/project-score-card'
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

  const sponsorNames = getSortedNameList(projectData?.projectSponsors ?? [])

  const ownerNames = getSortedNameList(projectData?.projectOwners ?? [])

  const managerNames = getSortedNameList(projectData?.projectManagers ?? [])

  const memberNames = getSortedNameList(projectData?.projectMembers ?? [])

  const strategicThemes = getSortedNameList(projectData?.strategicThemes ?? [])

  const sortedStrategicInitiatives = [
    ...(projectData?.strategicInitiatives ?? []),
  ].sort((a, b) => a.name.localeCompare(b.name))

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
          {projectData?.program && (
            <LabeledContent label="Program">
              <Link href={`/ppm/programs/${projectData.program.key}`}>
                {projectData.program.name}
              </Link>
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

          {strategicThemes.length > 0 && (
            <LabeledContent label="Strategic Themes">
              {strategicThemes.join(', ')}
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

          <Divider size="small" />

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

        {(projectData?.phases?.length ?? 0) > 0 && (
          <PhaseTimeline phases={projectData!.phases} />
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

