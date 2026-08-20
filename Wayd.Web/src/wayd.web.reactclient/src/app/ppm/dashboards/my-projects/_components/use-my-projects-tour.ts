'use client'

import { useTourCompleted } from '@/src/hooks'
import { TourProps } from 'antd'
import { useRef } from 'react'

const TOUR_KEY = 'myProjectsDashboard'

export interface MyProjectsTourRefs {
  filterBarRef: React.RefObject<HTMLDivElement | null>
  summaryBarRef: React.RefObject<HTMLDivElement | null>
  leftPanelRef: React.RefObject<HTMLDivElement | null>
  rightPanelRef: React.RefObject<HTMLDivElement | null>
}

export interface MyProjectsTourResult {
  refs: MyProjectsTourRefs
  tourOpen: boolean
  tourSteps: TourProps['steps']
  onTourClose: () => void
  onTourStart: () => void
  /** The step index for the "Project Details" step, used to auto-select a project */
  detailStepIndex: number
}

export const useMyProjectsTour = (): MyProjectsTourResult => {
  const { isCompleted, isLoading, markCompleted, resetTour } =
    useTourCompleted(TOUR_KEY)

  const filterBarRef = useRef<HTMLDivElement>(null)
  const summaryBarRef = useRef<HTMLDivElement>(null)
  const leftPanelRef = useRef<HTMLDivElement>(null)
  const rightPanelRef = useRef<HTMLDivElement>(null)

  const tourOpen = !isLoading && !isCompleted

  const detailStepIndex = 4

  const stepStyle: React.CSSProperties = { maxWidth: 360 }

  const tourSteps: TourProps['steps'] = [
    {
      title: 'Welcome to My Projects',
      description:
        "This dashboard gives you a personalized view of all projects you are involved in. Let's walk through the key areas.",
      cover: null,
      target: null,
      style: stepStyle,
    },
    {
      title: 'Filter Your Projects',
      description:
        'Use these filters to narrow down projects by your role (Sponsor, Owner, PM, Member, or Task Assignee) and by project status. Leadership roles (Sponsor, Owner, PM) show all tasks, while Member and Task Assignee show only your assigned tasks. Your filter selections are saved automatically.',
      target: (() => filterBarRef.current) as () => HTMLElement,
      placement: 'bottom',
      style: stepStyle,
    },
    {
      title: 'Summary Metrics',
      description:
        'See aggregated task metrics across your visible projects at a glance. These counts update based on your role and status filters.',
      target: (() => summaryBarRef.current) as () => HTMLElement,
      placement: 'bottom',
      style: stepStyle,
    },
    {
      title: 'Project List',
      description:
        'Your projects are grouped by portfolio. Each card shows your role, project status, stage timeline, task statistics, and team members. Task statistics on each card reflect your role filters, not your actual roles on the project.',
      target: (() => leftPanelRef.current) as () => HTMLElement,
      placement: 'right',
      style: stepStyle,
    },
    {
      title: 'Project Details',
      description:
        'Clicking a project card opens its details here, including stages, task summary, and the full project plan with deliverables and tasks. Click any task row to view additional task details.',
      target: (() => rightPanelRef.current) as () => HTMLElement,
      placement: 'left',
      style: stepStyle,
    },
  ]

  return {
    refs: { filterBarRef, summaryBarRef, leftPanelRef, rightPanelRef },
    tourOpen,
    tourSteps,
    onTourClose: markCompleted,
    onTourStart: resetTour,
    detailStepIndex,
  }
}

