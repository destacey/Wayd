'use client'

import { RightOutlined } from '@ant-design/icons'
import { Flex, Typography } from 'antd'
import { FC, useState } from 'react'
import { ProjectPlanSummaryDto } from '@/src/services/wayd-api'
import { PortfolioGroup } from './project-card-helpers'
import ProjectCard from './project-card'
import styles from '../my-projects-dashboard.module.css'

const { Text } = Typography

export interface PortfolioGroupSectionProps {
  group: PortfolioGroup
  selectedProjectKey: string | null
  employeeId: string | null
  planSummaries?: Record<string, ProjectPlanSummaryDto>
  onSelectProject: (key: string) => void
}

const PortfolioGroupSection: FC<PortfolioGroupSectionProps> = ({
  group,
  selectedProjectKey,
  employeeId,
  planSummaries,
  onSelectProject,
}) => {
  const [collapsed, setCollapsed] = useState(false)

  return (
    <div className={styles.portfolioGroup}>
      <div
        className={styles.portfolioHeader}
        onClick={() => setCollapsed((c) => !c)}
      >
        <RightOutlined
          className={`${styles.collapseIcon} ${!collapsed ? styles.collapseIconExpanded : ''}`}
        />
        <span className={styles.portfolioName}>{group.portfolioName}</span>
        <div className={styles.portfolioHeaderRight}>
          <span className={styles.portfolioCount}>
            {group.projects.length}{' '}
            {group.projects.length === 1 ? 'project' : 'projects'}
          </span>
        </div>
      </div>
      {!collapsed && (
        <Flex vertical gap={8}>
          {group.projects.map((project) => (
            <ProjectCard
              key={project.key}
              project={project}
              isSelected={selectedProjectKey === project.key}
              employeeId={employeeId}
              planSummary={planSummaries?.[project.id]}
              onSelect={onSelectProject}
            />
          ))}
        </Flex>
      )}
    </div>
  )
}

export default PortfolioGroupSection
