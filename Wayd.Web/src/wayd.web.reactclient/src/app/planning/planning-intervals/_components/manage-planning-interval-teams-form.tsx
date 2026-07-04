'use client'

import { useConfirmModal } from '@/src/hooks'
import { TeamListItem } from '@/src/app/organizations/types'
import {
  getPlanningIntervalsClient,
  getTeamsClient,
  getTeamsOfTeamsClient,
} from '@/src/services/clients'
import { Modal, Spin } from 'antd'
import { useCallback, useEffect, useState } from 'react'
import type { ColumnDef } from '@tanstack/react-table'
import WaydGridTransfer from '@/src/components/common/wayd-grid-transfer'
import {
  ManagePlanningIntervalTeamsRequest,
  PlanningIntervalTeamResponse,
} from '@/src/services/wayd-api'
import { useMessage } from '@/src/components/contexts/messaging'

export interface ManagePlanningIntervalTeamsFormProps {
  id: string
  onFormSave: () => void
  onFormCancel: () => void
}

interface PlanningIntervalTeamModel {
  key: string
  name: string
  code: string
  teamOfTeams: string | undefined
}

const teamColumns: ColumnDef<PlanningIntervalTeamModel, any>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    size: 220,
  },
  {
    accessorKey: 'code',
    header: 'Code',
    size: 110,
  },
  {
    accessorKey: 'teamOfTeams',
    header: 'Team of Teams',
    size: 220,
    meta: { filterType: 'set' },
  },
]

const defaultSort = (
  a: PlanningIntervalTeamModel,
  b: PlanningIntervalTeamModel,
) => a.name.localeCompare(b.name)

const ManagePlanningIntervalTeamsForm = ({
  id,
  onFormSave,
  onFormCancel,
}: ManagePlanningIntervalTeamsFormProps) => {
  const [isLoading, setIsLoading] = useState(false)
  const [teams, setTeams] = useState<PlanningIntervalTeamModel[]>([])
  const [targetKeys, setTargetKeys] = useState<string[]>([])
  const messageApi = useMessage()

  // TODO: should this be in a custom hook? The teams index page has a similar call.
  const getTeams = useCallback(async () => {
    const [teamsDtos, teamOfTeamsDtos] = await Promise.all([
      getTeamsClient().getList(false),
      getTeamsOfTeamsClient().getList(false),
    ])

    return [...teamsDtos, ...teamOfTeamsDtos].map((team: TeamListItem) => ({
      key: team.id,
      name: team.name,
      code: team.code ?? '',
      teamOfTeams: team.teamOfTeams?.name,
    }))
  }, [])

  const getPlanningIntervalTeams = useCallback(async (id: string) => {
    const piTeamsDtos = await getPlanningIntervalsClient().getTeams(id)

    return piTeamsDtos.map((team: PlanningIntervalTeamResponse) => ({
      key: team.id,
      name: team.name,
      code: team.code,
      teamOfTeams: team.teamOfTeams?.name,
    }))
  }, [])

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const request: ManagePlanningIntervalTeamsRequest = {
          id: id,
          teamIds: targetKeys,
        }
        await getPlanningIntervalsClient().manageTeams(id, request)

        messageApi.success(`Successfully updated PI teams.`)
        return true
      } catch (error) {
        messageApi.error(`An unexpected error occurred while saving.`)
        console.error(error)
        return false
      }
    },
    onComplete: onFormSave,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while updating PI teams. Please try again.',
    permission: 'Permissions.PlanningIntervals.Update',
  })

  useEffect(() => {
    if (!isOpen) return
    let cancelled = false

    const loadData = async () => {
      try {
        setIsLoading(true)
        const [teamsData, piTeamsData] = await Promise.all([
          getTeams(),
          getPlanningIntervalTeams(id),
        ])
        if (!cancelled) {
          setTeams(teamsData)
          setTargetKeys(piTeamsData.map((team) => team.key))
        }
      } catch (error) {
        if (!cancelled) {
          messageApi.error(
            `An unexpected error occurred while retrieving PI teams.`,
          )
          console.error(error)
        }
      }
      if (!cancelled) {
        setIsLoading(false)
      }
    }

    loadData()
    return () => {
      cancelled = true
    }
  }, [isOpen, id, getTeams, getPlanningIntervalTeams, messageApi])

  const targetKeySet = new Set(targetKeys)
  const sourceTeams = teams
    .filter((team) => !targetKeySet.has(team.key))
    .sort(defaultSort)
  const targetTeams = teams
    .filter((team) => targetKeySet.has(team.key))
    .sort(defaultSort)

  const handleMove = (items: PlanningIntervalTeamModel[]) => {
    if (items.length === 0) return

    setTargetKeys((prev) => [...prev, ...items.map((item) => item.key)])
  }

  const handleRemove = (item: PlanningIntervalTeamModel) => {
    if (!item) return

    setTargetKeys((prev) => prev.filter((key) => key !== item.key))
  }

  return (
    <Modal
      title="Manage Planning Interval Teams"
      open={isOpen}
      width={'80vw'}
      onOk={handleOk}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {
        <Spin spinning={isLoading} size="large">
          <WaydGridTransfer
            leftData={sourceTeams}
            rightData={targetTeams}
            columns={teamColumns}
            getRowId={(item) => item.key}
            getDragLabel={(item) => item.code || item.name}
            onMove={handleMove}
            onRemove={handleRemove}
          />
        </Spin>
      }
    </Modal>
  )
}

export default ManagePlanningIntervalTeamsForm
