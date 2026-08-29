'use client'

import { ProjectDetailsDto } from '@/src/services/wayd-api'
import { FC } from 'react'
import { ProjectPlanTable } from '.'
import { useGetProjectPlanTreeQuery } from '@/src/store/features/ppm/projects-api'

export interface ProjectPlanProps {
  project: ProjectDetailsDto
  canManageTasks: boolean
}

const ProjectPlan: FC<ProjectPlanProps> = ({
  project,
  canManageTasks,
}: ProjectPlanProps) => {
  const {
    data: planData,
    isLoading,
    refetch,
  } = useGetProjectPlanTreeQuery(project?.key || '', {
    skip: !project?.key || !project?.projectLifecycle,
  })

  if (!project) return null

  return (
    <ProjectPlanTable
      projectId={project.id}
      projectKey={project.key}
      tasks={planData ?? []}
      isLoading={isLoading}
      canManageTasks={canManageTasks}
      refetch={refetch}
    />
  )
}

export default ProjectPlan
