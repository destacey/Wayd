'use client'

import { useState } from 'react'
import { Alert, Flex, InputNumber, Modal, Spin, Table, Tag } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  CalendarDate,
  formatCalendarDate,
  isApiError,
  type ApiError,
} from '@/src/utils'
import { useChangeEmployeeAllocationsMutation } from '@/src/store/features/organization/employee-allocations-api'
import {
  CapacityTeamType,
  TeamAllocationProposalRowDto,
  useGetTeamAllocationProposalQuery,
} from '@/src/store/features/organization/team-capacity-api'
import { overProposed, proposalRequests } from './team-capacity-data'

interface Props {
  teamId: string
  teamType: CapacityTeamType
  /** The first day the proposed allocations cover. */
  from: CalendarDate
  onFormComplete: () => void
  onFormCancel: () => void
}

const rowKey = (row: TeamAllocationProposalRowDto) =>
  `${row.employee.id}|${row.role.id}`

interface ProposalProps extends Omit<Props, 'teamType'> {
  rows: TeamAllocationProposalRowDto[]
}

/** A person whose proposed allocations the API refused, and why. */
interface Failure {
  employeeId: string
  name: string
  reason: string
}

/**
 * The proposal as the planner edits it. Mounted once the proposal has loaded,
 * so its percents can start from the proposed ones.
 */
const Proposal = ({
  teamId,
  from,
  rows: proposedRows,
  onFormComplete,
  onFormCancel,
}: ProposalProps) => {
  const messageApi = useMessage()
  const [changeAllocations] = useChangeEmployeeAllocationsMutation()
  const [isSaving, setIsSaving] = useState(false)
  // After a save only the people it failed for are left, to correct and retry.
  const [rows, setRows] = useState(proposedRows)
  const [failures, setFailures] = useState<Failure[]>([])
  const [percents, setPercents] = useState<Record<string, number>>(() =>
    Object.fromEntries(proposedRows.map((row) => [rowKey(row), row.percent])),
  )

  const proposed = rows.map((row) => ({
    employeeId: row.employee.id,
    roleId: row.role.id,
    end: row.end,
    remainingPercent: row.remainingPercent,
    percent: percents[rowKey(row)] ?? 0,
  }))
  const over = overProposed(proposed)
  const requests = proposalRequests(proposed, teamId, from)
  const nothingLeft = rows.some((row) => row.remainingPercent === 0)

  const save = async () => {
    setIsSaving(true)
    const failed: Failure[] = []
    for (const request of requests) {
      const response = await changeAllocations(request)
      if (response.error) {
        const apiError: ApiError = isApiError(response.error)
          ? response.error
          : {}
        const person = rows.find((r) => r.employee.id === request.employeeId)
        failed.push({
          employeeId: request.employeeId,
          name: person?.employee.name ?? request.employeeId,
          reason: apiError.detail ?? 'The allocation could not be saved.',
        })
      }
    }
    setIsSaving(false)

    if (failed.length === 0) {
      messageApi.success('Allocations saved.')
      onFormComplete()
      return
    }

    // The range is no longer empty once anyone is saved, so this form will
    // not be offered again: it stays open on the people still to sort out.
    const failedIds = new Set(failed.map((f) => f.employeeId))
    setRows(rows.filter((row) => failedIds.has(row.employee.id)))
    setFailures(failed)
  }

  const hasEnds = rows.some((row) => row.end)

  const columns: ColumnsType<TeamAllocationProposalRowDto> = [
    { title: 'Person', key: 'person', render: (_, row) => row.employee.name },
    { title: 'Role', key: 'role', render: (_, row) => row.role.name },
    ...(hasEnds
      ? [
          {
            title: 'Ends',
            key: 'end',
            render: (_: unknown, row: TeamAllocationProposalRowDto) =>
              row.end ? formatCalendarDate(row.end) : 'No end',
          },
        ]
      : []),
    {
      title: 'Left to allocate',
      key: 'remaining',
      render: (_, row) =>
        row.remainingPercent === 0 ? (
          <Tag color="warning">Nothing left</Tag>
        ) : (
          `${row.remainingPercent}%`
        ),
    },
    {
      title: 'Percent',
      key: 'percent',
      render: (_, row) => (
        <InputNumber
          size="small"
          min={0}
          max={100}
          precision={0}
          suffix="%"
          aria-label={`Percent for ${row.employee.name} as ${row.role.name}`}
          status={over.has(row.employee.id) ? 'error' : undefined}
          disabled={row.remainingPercent === 0}
          value={percents[rowKey(row)]}
          onChange={(value) =>
            setPercents((current) => ({
              ...current,
              [rowKey(row)]: value ?? 0,
            }))
          }
        />
      ),
    },
  ]

  return (
    <Modal
      title="Initialize Allocations"
      open
      width={720}
      okText="Save"
      onOk={save}
      okButtonProps={{ disabled: requests.length === 0 || over.size > 0 }}
      confirmLoading={isSaving}
      onCancel={onFormCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap="middle">
        <Alert
          type="info"
          showIcon
          title={`Each member is proposed at what is left of their time from ${formatCalendarDate(from)}. Nothing is saved until you choose Save.`}
          description={
            hasEnds
              ? 'A row with an end date stops the day before time the team already has from that person, which is left as it is.'
              : undefined
          }
        />
        {failures.length > 0 && (
          <Alert
            type="error"
            showIcon
            title="These people could not be allocated. Everyone else was saved."
            description={
              <ul>
                {failures.map((failure) => (
                  <li key={failure.employeeId}>
                    {`${failure.name}: ${failure.reason}`}
                  </li>
                ))}
              </ul>
            }
          />
        )}
        {nothingLeft && (
          <Alert
            type="warning"
            showIcon
            title="Some members have nothing left to allocate. Move time from another team to give them some here."
          />
        )}
        {over.size > 0 && (
          <Alert
            type="error"
            showIcon
            title="A person's rows add up to more than they have left."
          />
        )}
        <Table
          size="small"
          rowKey={rowKey}
          columns={columns}
          dataSource={rows}
          pagination={false}
          locale={{ emptyText: 'This team has no active members to propose.' }}
        />
      </Flex>
    </Modal>
  )
}

/**
 * Offers a team's allocations from its members. The proposal is read fresh
 * each time, and saved only through the change allocations endpoint once the
 * planner confirms.
 */
const InitializeAllocationsForm = (props: Props) => {
  const { teamId, teamType, from, onFormCancel } = props
  const { data, isFetching, error } = useGetTeamAllocationProposalQuery(
    { teamId, teamType, from },
    { refetchOnMountOrArgChange: true },
  )

  if (isFetching || !data) {
    return (
      <Modal
        title="Initialize Allocations"
        open
        footer={null}
        onCancel={onFormCancel}
      >
        {error ? (
          <Alert
            type="error"
            showIcon
            title="The proposal could not be loaded."
          />
        ) : (
          <Spin />
        )}
      </Modal>
    )
  }

  return <Proposal {...props} rows={data.rows} />
}

export default InitializeAllocationsForm
