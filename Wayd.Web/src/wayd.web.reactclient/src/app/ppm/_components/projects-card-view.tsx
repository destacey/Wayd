'use client'

import { LifecycleStatusTag } from '@/src/components/common'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import PhaseTimeline from './phase-timeline'
import { ProjectListDto } from '@/src/services/wayd-api'
import { getSortedNames } from '@/src/utils'
import { Card, Flex, Segmented, Spin, Typography } from 'antd'
import styles from './projects-card-view.module.css'
import Link from 'next/link'
import { FC, ReactNode, useMemo, useState } from 'react'
import { ProjectHealthCheckTag } from '../projects/_components'

const { Text } = Typography
type SortMode = 'name' | 'rank'

const compareProjectNames = (a: ProjectListDto, b: ProjectListDto) =>
  a.name.localeCompare(b.name, undefined, {
    numeric: true,
    sensitivity: 'base',
  })

const compareProjectRanks = (a: ProjectListDto, b: ProjectListDto) => {
  const aPosition = a.position ?? Number.MAX_SAFE_INTEGER
  const bPosition = b.position ?? Number.MAX_SAFE_INTEGER
  const positionDiff = aPosition - bPosition

  if (positionDiff !== 0) return positionDiff

  return compareProjectNames(a, b)
}

interface ProjectCardProps {
  project: ProjectListDto
  onCardClick: (key: string) => void
  hidePortfolio?: boolean
}

const ProjectCard: FC<ProjectCardProps> = ({ project, onCardClick, hidePortfolio }) => {
  const managerNames = getSortedNames(project.projectManagers)

  const timelineFormat =
    project.start &&
    project.end &&
    new Date(project.start).getFullYear() === new Date().getFullYear()
      ? 'MMM D'
      : 'MMM D, YYYY'

  return (
    <Card
      size="small"
      hoverable
      className={styles.card}
      onClick={() => onCardClick(project.key)}
    >
      <Flex vertical gap={1} style={{ flex: 1 }}>
        {/* Header */}
        <Flex justify="space-between" gap="small">
          <Text type="secondary" style={{ fontSize: 11 }}>
            {project.key}
          </Text>

          <Flex align="center" gap="small">
            <ProjectHealthCheckTag
              healthCheck={project.healthCheck}
              projectId={project.id}
              variant="flag"
            />
            <LifecycleStatusTag status={project.status} />
          </Flex>
        </Flex>

        <Link
          href={`/ppm/projects/${project.key}`}
          onClick={(e) => e.stopPropagation()}
          style={{ width: 'fit-content' }}
        >
          {project.name}
        </Link>

        {/* Meta rows */}
        <Flex vertical gap={3}>
          {!hidePortfolio && (
            <Flex gap={6} align="center">
              <Text type="secondary" style={{ fontSize: 11, minWidth: 60 }}>
                Portfolio
              </Text>
              <Text
                style={{ fontSize: 12 }}
                ellipsis={{ tooltip: project.portfolio.name }}
              >
                {project.portfolio.name}
              </Text>
            </Flex>
          )}
          <Flex gap={6} align="center">
            <Text type="secondary" style={{ fontSize: 11, minWidth: 60 }}>
              Managers
            </Text>
            <Text style={{ fontSize: 12 }} ellipsis={{ tooltip: managerNames }}>
              {managerNames || 'No manager assigned'}
            </Text>
          </Flex>
        </Flex>

        {/* Phases */}
        {project.phases?.length > 0 ? (
          <PhaseTimeline phases={project.phases} displayMode="small" />
        ) : (
          <Text type="secondary" style={{ fontSize: 12 }}>
            No lifecycle defined
          </Text>
        )}

        {/* Timeline */}
        {project.start && project.end && (
          <TimelineProgress
            start={project.start}
            end={project.end}
            variant="borderless"
            size="small"
            style={{ width: '100%', marginTop: 'auto' }}
            dateFormat={timelineFormat}
          />
        )}
      </Flex>
    </Card>
  )
}

export interface ProjectsCardViewProps {
  projects: ProjectListDto[] | undefined
  isLoading: boolean
  viewSelector?: ReactNode
  onCardClick: (key: string) => void
  hidePortfolio?: boolean
}

const ProjectsCardView: FC<ProjectsCardViewProps> = ({
  projects,
  isLoading,
  viewSelector,
  onCardClick,
  hidePortfolio,
}) => {
  const [sortMode, setSortMode] = useState<SortMode>('name')

  const sortedProjects = useMemo(() => {
    const comparer =
      sortMode === 'rank' ? compareProjectRanks : compareProjectNames

    return [...(projects ?? [])].sort(comparer)
  }, [projects, sortMode])

  const toolbar = (
    <Flex
      align="center"
      justify="space-between"
      gap="small"
      wrap="wrap"
      className={styles.toolbar}
    >
      <Segmented
        size="small"
        value={sortMode}
        onChange={(value) => setSortMode(value as SortMode)}
        options={[
          { label: 'by name', value: 'name' },
          { label: 'by rank', value: 'rank' },
        ]}
      />
      {viewSelector}
    </Flex>
  )

  if (isLoading) {
    return (
      <Flex vertical gap="small">
        {toolbar}
        <Flex justify="center" style={{ padding: 24 }}>
          <Spin />
        </Flex>
      </Flex>
    )
  }

  return (
    <Flex vertical gap="small">
      {toolbar}
      <div className={styles.grid}>
        {sortedProjects.map((project) => (
          <ProjectCard
            key={project.key}
            project={project}
            onCardClick={onCardClick}
            hidePortfolio={hidePortfolio}
          />
        ))}
      </div>
    </Flex>
  )
}

export default ProjectsCardView
