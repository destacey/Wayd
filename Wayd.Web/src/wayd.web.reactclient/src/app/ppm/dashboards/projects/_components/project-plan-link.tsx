'use client'

import { ProjectOutlined } from '@ant-design/icons'
import { WaydTooltip } from '@/src/components/common'
import { ProjectListDto } from '@/src/services/wayd-api'
import Link from 'next/link'
import { FC, MouseEvent } from 'react'
import styles from '../projects-dashboard.module.css'

/** The project page's Plan section, as its `?section=` value names it. */
export const projectPlanHref = (project: ProjectListDto) =>
  `/ppm/projects/${project.key}?section=plan`

/**
 * One click to a project's plan. Nothing renders for a project without a
 * lifecycle, since the Plan section only exists once stages can be laid out.
 * The click stays on the link: inside an activatable row or card it must not
 * also open the drawer.
 */
const ProjectPlanLink: FC<{ project: ProjectListDto }> = ({ project }) => {
  if (!project.projectLifecycle) return null

  const stop = (e: MouseEvent) => e.stopPropagation()

  return (
    <WaydTooltip title="Open plan">
      <Link
        href={projectPlanHref(project)}
        aria-label={`Open plan for ${project.key}`}
        className={styles.planLink}
        onClick={stop}
      >
        <ProjectOutlined />
      </Link>
    </WaydTooltip>
  )
}

export default ProjectPlanLink
