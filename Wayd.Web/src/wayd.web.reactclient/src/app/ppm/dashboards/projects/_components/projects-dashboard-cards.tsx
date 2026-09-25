'use client'

import { LifecycleStatusTag, WaydEmpty } from '@/src/components/common'
import StageTimeline from '@/src/app/ppm/_components/stage-timeline'
import ProjectHealthCheckTag from '@/src/app/ppm/projects/_components/project-health-check-tag'
import { ProjectListDto } from '@/src/services/wayd-api'
import { Card, Col, Flex, Row, Skeleton, Typography } from 'antd'
import { Dayjs } from 'dayjs'
import { FC, useState } from 'react'
import DashboardGroupHeader from './dashboard-group-header'
import {
  collectLeadership,
  formatEnd,
  getEmployeeRoles,
  isEndingSoon,
  PlanSummaries,
  ProjectGroup,
} from './dashboard-model'
import ProjectPlanLink from './project-plan-link'
import ProjectStatPills from './project-stat-pills'
import TeamAvatars from './team-avatars'
import styles from '../projects-dashboard.module.css'

const { Text } = Typography

export interface ProjectsDashboardCardsProps {
  groups: ProjectGroup[]
  planSummaries: PlanSummaries
  /** Whose roles the card names. Null leaves roles off. */
  employeeId: string | null
  selectedProjectKey: string | null
  onSelectProject: (key: string) => void
  isLoading: boolean
  today: Dayjs
  /** Fixed height for the scroll container; unset lets it grow with its cards. */
  height?: number
}

interface CardProps {
  project: ProjectListDto
  planSummaries: PlanSummaries
  employeeId: string | null
  isSelected: boolean
  onSelect: (key: string) => void
  today: Dayjs
}

const ProjectCard: FC<CardProps> = ({
  project,
  planSummaries,
  employeeId,
  isSelected,
  onSelect,
  today,
}) => {
  const roles = getEmployeeRoles(project, employeeId)
  const sub = project.program
    ? `${project.portfolio.name} · ${project.program.name}`
    : project.portfolio.name
  const endingSoon = isEndingSoon(project, today)
  const end = formatEnd(project.end)

  return (
    <Card
      size="small"
      hoverable
      className={`${styles.card} ${isSelected ? styles.cardSelected : ''}`}
      onClick={() => onSelect(project.key)}
      role="button"
      aria-pressed={isSelected}
      aria-label={`${project.key} ${project.name}`}
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onSelect(project.key)
        }
      }}
    >
      <Flex vertical gap={8}>
        <Flex align="center" justify="space-between" gap={8}>
          <ProjectHealthCheckTag
            healthCheck={project.healthCheck}
            projectId={project.id}
            variant="tag"
          />
          <LifecycleStatusTag status={project.status} />
        </Flex>
        <Flex vertical gap={0}>
          <span className={styles.projectSub}>
            {project.key}
            {employeeId && (
              <> · {roles.length > 0 ? roles.join(' · ') : 'Task Assignee'}</>
            )}
          </span>
          <Text className={styles.cardName} ellipsis>
            {project.name}
          </Text>
          <span className={styles.projectSub}>{sub}</span>
        </Flex>
        {project.stages?.length > 0 ? (
          <StageTimeline stages={project.stages} displayMode="small" />
        ) : (
          <span className={`${styles.projectSub} ${styles.muted}`}>
            No lifecycle
          </span>
        )}
        <Flex align="center" justify="space-between" gap={8}>
          <ProjectStatPills summary={planSummaries[project.id]} />
          <TeamAvatars members={collectLeadership(project)} max={3} />
        </Flex>
        <Flex justify="space-between" className={styles.projectSub}>
          <span>
            Ends{' '}
            <span className={endingSoon ? styles.endDateSoon : undefined}>
              {end ?? '—'}
            </span>
          </span>
          <Flex align="center" gap={8}>
            <span>
              Score{' '}
              {project.currentScore
                ? project.currentScore.value.toFixed(1)
                : '—'}
            </span>
            <ProjectPlanLink project={project} />
          </Flex>
        </Flex>
      </Flex>
    </Card>
  )
}

interface GroupSectionProps extends Omit<CardProps, 'project' | 'isSelected'> {
  group: ProjectGroup
  selectedProjectKey: string | null
}

const GroupSection: FC<GroupSectionProps> = ({
  group,
  selectedProjectKey,
  ...cardProps
}) => {
  const [collapsed, setCollapsed] = useState(false)

  return (
    <div className={styles.cardGroup}>
      <DashboardGroupHeader
        group={group}
        collapsed={collapsed}
        onToggle={() => setCollapsed((c) => !c)}
      />
      {!collapsed && (
        <Row gutter={[12, 12]} className={styles.cardGrid}>
          {group.projects.map((project) => (
            <Col key={project.key} xs={24} sm={12} lg={8} xl={6}>
              <ProjectCard
                project={project}
                isSelected={selectedProjectKey === project.key}
                {...cardProps}
              />
            </Col>
          ))}
        </Row>
      )}
    </div>
  )
}

/** The same groups and data as the list, as a card per project. */
const ProjectsDashboardCards: FC<ProjectsDashboardCardsProps> = ({
  groups,
  planSummaries,
  employeeId,
  selectedProjectKey,
  onSelectProject,
  isLoading,
  today,
  height,
}) => {
  if (isLoading) {
    return (
      <div className={styles.list} style={{ padding: 16 }}>
        <Skeleton active paragraph={{ rows: 6 }} />
      </div>
    )
  }

  if (groups.length === 0) {
    return (
      <div className={`${styles.list} ${styles.emptyState}`}>
        <WaydEmpty message="No projects match the current scope and filters." />
      </div>
    )
  }

  return (
    <div className={styles.list} style={{ height }}>
      {groups.map((group) => (
        <GroupSection
          key={group.key}
          group={group}
          selectedProjectKey={selectedProjectKey}
          planSummaries={planSummaries}
          employeeId={employeeId}
          onSelect={onSelectProject}
          today={today}
        />
      ))}
    </div>
  )
}

export default ProjectsDashboardCards
