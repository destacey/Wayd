'use client'

import { WaydEmpty } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { ProjectListDto } from '@/src/services/wayd-api'
import { useDebounce } from '@/src/hooks'
import { useGetProjectsPlanSummariesQuery } from '@/src/store/features/ppm/projects-api'
import { SearchOutlined } from '@ant-design/icons'
import { Flex, Input, Spin } from 'antd'
import { FC, useState } from 'react'
import { PortfolioGroup, sortProjects } from './project-card-helpers'
import PortfolioGroupSection from './portfolio-group-section'
import styles from '../my-projects-dashboard.module.css'

export interface PortfolioGroupListProps {
  projects: ProjectListDto[] | undefined
  isLoading: boolean
  selectedProjectKey: string | null
  selectedRoles: number[]
  onSelectProject: (key: string) => void
}

const PortfolioGroupList: FC<PortfolioGroupListProps> = ({
  projects,
  isLoading,
  selectedProjectKey,
  selectedRoles,
  onSelectProject,
}) => {
  const { user } = useAuth()
  const [searchTerm, setSearchTerm] = useState('')
  const debouncedSearch = useDebounce(searchTerm, 300)

  const projectIds = projects?.map((p) => p.id) ?? []

  const { data: planSummaries } = useGetProjectsPlanSummariesQuery(
    {
      projectIds,
      role: selectedRoles.length > 0 ? selectedRoles : undefined,
    },
    { skip: projectIds.length === 0 },
  )

  const groups: PortfolioGroup[] = (() => {
    if (!projects) return []

    const needle = debouncedSearch.toLowerCase().trim()
    const filtered = needle
      ? projects.filter((p) => p.name.toLowerCase().includes(needle))
      : projects

    const map = new Map<string, PortfolioGroup>()
    for (const project of filtered) {
      const portfolioId = project.portfolio?.id ?? 'unassigned'
      const portfolioName = project.portfolio?.name ?? 'Unassigned'
      if (!map.has(portfolioId)) {
        map.set(portfolioId, { portfolioId, portfolioName, projects: [] })
      }
      map.get(portfolioId)!.projects.push(project)
    }

    return Array.from(map.values())
      .map((group) => ({
        ...group,
        projects: sortProjects(group.projects),
      }))
      .sort((a, b) => a.portfolioName.localeCompare(b.portfolioName))
  })()

  if (isLoading) {
    return (
      <Flex justify="center" className={styles.emptyState}>
        <Spin />
      </Flex>
    )
  }

  if (!projects || projects.length === 0) {
    return <WaydEmpty message="No projects found" />
  }

  return (
    <Flex vertical gap={4}>
      <Input
        prefix={<SearchOutlined />}
        placeholder="Search projects..."
        allowClear
        value={searchTerm}
        onChange={(e) => setSearchTerm(e.target.value)}
        size="small"
      />
      {groups.length === 0 ? (
        <WaydEmpty message="No matching projects" />
      ) : (
        groups.map((group) => (
          <PortfolioGroupSection
            key={group.portfolioId}
            group={group}
            selectedProjectKey={selectedProjectKey}
            employeeId={user?.employeeId ?? null}
            planSummaries={planSummaries}
            onSelectProject={onSelectProject}
          />
        ))
      )}
    </Flex>
  )
}

export default PortfolioGroupList
