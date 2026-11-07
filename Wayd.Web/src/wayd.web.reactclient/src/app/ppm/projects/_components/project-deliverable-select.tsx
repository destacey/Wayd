'use client'

import { StatusCategory } from '@/src/services/wayd-api'
import { useGetProjectDeliverablesQuery } from '@/src/store/features/ppm/project-deliverables-api'
import { Select } from 'antd'

export interface ProjectDeliverableSelectProps {
  /** The project's id: deliverables are read by id, which is what their cache is keyed on. */
  projectId: string
  value?: string[]
  onChange?: (value: string[]) => void
  id?: string
}

/**
 * Picks the deliverables of one project a task serves, for use as a form
 * field, in the deliverables' own order. Descoped deliverables are offered only while already chosen, so a
 * task can be unlinked from one but not newly linked to it.
 */
const ProjectDeliverableSelect = ({
  projectId,
  value,
  onChange,
  id,
}: ProjectDeliverableSelectProps) => {
  const { data: deliverables = [], isLoading } =
    useGetProjectDeliverablesQuery(projectId)

  const chosen = new Set(value ?? [])
  const options = deliverables
    .filter(
      (d) => d.status.category !== StatusCategory.Removed || chosen.has(d.id),
    )
    .map((d) => ({
      value: d.id,
      label:
        d.status.category === StatusCategory.Removed
          ? `${d.name} (${d.status.name})`
          : d.name,
    }))

  return (
    <Select
      id={id}
      mode="multiple"
      allowClear
      loading={isLoading}
      placeholder="Select deliverables (optional)"
      notFoundContent="This project has no deliverables"
      options={options}
      value={value}
      onChange={onChange}
      optionFilterProp="label"
    />
  )
}

export default ProjectDeliverableSelect
