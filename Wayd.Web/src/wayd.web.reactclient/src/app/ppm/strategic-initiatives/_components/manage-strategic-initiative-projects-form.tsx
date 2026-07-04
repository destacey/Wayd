'use client'

import WaydGridTransfer from '@/src/components/common/wayd-grid-transfer'
import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import {
  ManageStrategicInitiativeProjectsRequest,
  ProjectListDto,
} from '@/src/services/wayd-api'
import { useGetPortfolioProjectsQuery } from '@/src/store/features/ppm/portfolios-api'
import { useGetProjectsQuery } from '@/src/store/features/ppm/projects-api'
import {
  useGetStrategicInitiativeProjectsQuery,
  useManageStrategicInitiativeProjectsMutation,
} from '@/src/store/features/ppm/strategic-initiatives-api'
import type { ColumnDef } from '@tanstack/react-table'
import { Checkbox, Flex, Modal } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { isApiError, type ApiError } from '@/src/utils'

export interface ManageStrategicInitiativeProjectsFormProps {
  strategicInitiativeId: string
  portfolioId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

const projectColumns: ColumnDef<ProjectListDto, any>[] = [
  {
    accessorKey: 'key',
    header: 'Key',
    size: 120,
  },
  {
    accessorKey: 'name',
    header: 'Name',
    size: 250,
  },
  {
    accessorKey: 'portfolio.name',
    header: 'Portfolio',
    size: 180,
    meta: { filterType: 'set' },
  },
  {
    accessorKey: 'status.name',
    header: 'Status',
    size: 100,
    meta: { filterType: 'set' },
  },
]

const defaultSort = (a: ProjectListDto, b: ProjectListDto) => {
  return a.name.localeCompare(b.name)
}

const ManageStrategicInitiativeProjectsForm = ({
  strategicInitiativeId,
  portfolioId,
  onFormComplete,
  onFormCancel,
}: ManageStrategicInitiativeProjectsFormProps) => {
  const [targetProjects, setTargetProjects] = useState<ProjectListDto[]>([])
  const [includeOtherPortfolios, setIncludeOtherPortfolios] = useState(false)

  const messageApi = useMessage()

  const {
    data: existingProjectsData,
    isLoading: existingProjectsIsLoading,
    error: existingProjectsError,
  } = useGetStrategicInitiativeProjectsQuery(strategicInitiativeId)

  const {
    data: portfolioProjectsData,
    isLoading: portfolioProjectsIsLoading,
    error: portfolioProjectsError,
  } = useGetPortfolioProjectsQuery(
    { portfolioIdOrKey: portfolioId },
    { skip: includeOtherPortfolios },
  )

  const {
    data: allProjectsData,
    isLoading: allProjectsIsLoading,
    error: allProjectsError,
  } = useGetProjectsQuery(undefined, { skip: !includeOtherPortfolios })

  const projectData = includeOtherPortfolios
    ? allProjectsData
    : portfolioProjectsData
  const projectsIsLoading = includeOtherPortfolios
    ? allProjectsIsLoading
    : portfolioProjectsIsLoading
  const projectsError = includeOtherPortfolios
    ? allProjectsError
    : portfolioProjectsError

  const [manageProjects] = useManageStrategicInitiativeProjectsMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const request: ManageStrategicInitiativeProjectsRequest = {
          id: strategicInitiativeId,
          projectIds: targetProjects.map((item) => item.id),
        }

        const response = await manageProjects(request)
        if (response.error) throw response.error

        messageApi.success('Projects updated successfully.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An error occurred while updating the projects. Please try again.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while managing the projects. Please try again.',
    permission: 'Permissions.StrategicInitiatives.Update',
  })

  useEffect(() => {
    if (!existingProjectsData) return
    setTargetProjects(existingProjectsData?.slice().sort(defaultSort) ?? [])
  }, [existingProjectsData])

  const sourceProjects = useMemo(() => {
    if (!projectData) return []
    const targetIds = new Set(targetProjects.map((p) => p.id))
    return projectData
      .filter((p) => !targetIds.has(p.id))
      .sort(defaultSort)
  }, [projectData, targetProjects])

  const handleMove = (items: ProjectListDto[]) => {
    if (items.length === 0) return

    setTargetProjects((prevTarget) =>
      [...prevTarget, ...items].sort(defaultSort),
    )
  }

  const handleDelete = (item: ProjectListDto) => {
    if (!item) return

    setTargetProjects((prevTarget) =>
      prevTarget.filter((p) => p.id !== item.id),
    )
  }

  return (
    <Modal
      title="Manage Strategic Initiative Projects"
      open={isOpen}
      width={'80vw'}
      onOk={handleOk}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Flex vertical gap="small">
        <Checkbox
          checked={includeOtherPortfolios}
          onChange={(e) => setIncludeOtherPortfolios(e.target.checked)}
        >
          Include projects from other portfolios
        </Checkbox>
        <WaydGridTransfer
          leftData={sourceProjects}
          rightData={targetProjects}
          columns={projectColumns}
          getRowId={(item) => item.id}
          getDragLabel={(item) => item.key}
          onMove={handleMove}
          onRemove={handleDelete}
        />
      </Flex>
    </Modal>
  )
}

export default ManageStrategicInitiativeProjectsForm
