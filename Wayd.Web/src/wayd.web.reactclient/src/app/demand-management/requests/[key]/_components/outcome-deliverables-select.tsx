'use client'

import { useGetIntakeRequestProjectDeliverablesQuery } from '@/src/store/features/demand-management/demand-management-api'
import { Select } from 'antd'
import { FC } from 'react'
import { outcomeDeliverableStatusLabel } from '../../../_components/outcome-deliverable-status'

export interface OutcomeDeliverablesSelectProps {
  requestId: string
  projectId: string
  value?: string[]
  onChange?: (value: string[]) => void
  /** Set by the form item, so its label names the field. */
  id?: string
}

/**
 * Chooses deliverables of the project an existing project scope outcome points at, from Demand Management's
 * copy, so an agent needs no PPM permission to see them. Controlled by the form item around it.
 */
const OutcomeDeliverablesSelect: FC<OutcomeDeliverablesSelectProps> = ({
  requestId,
  projectId,
  value,
  onChange,
  id,
}) => {
  const { data, isFetching, isError } =
    useGetIntakeRequestProjectDeliverablesQuery({ requestId, projectId })

  // A mapped deliverable since deleted from the project is no longer offered, but stays selected by its id.
  const offered = new Set((data ?? []).map((d) => d.id))
  const options = [
    ...(data ?? []).map((d) => ({
      label: `${d.name} · ${outcomeDeliverableStatusLabel(d.statusAlias, d.statusCategory)}`,
      value: d.id,
    })),
    ...(value ?? [])
      .filter((v) => !offered.has(v))
      .map((v) => ({ label: 'Deleted from the project', value: v })),
  ]

  return (
    <Select
      id={id}
      mode="multiple"
      allowClear
      showSearch={{ optionFilterProp: 'label' }}
      placeholder={
        isError ? 'The deliverables could not be loaded' : 'Select deliverables'
      }
      loading={isFetching}
      status={isError ? 'error' : undefined}
      options={options}
      value={value}
      onChange={onChange}
      notFoundContent={
        isFetching ? undefined : 'This project has no deliverables yet.'
      }
    />
  )
}

export default OutcomeDeliverablesSelect
