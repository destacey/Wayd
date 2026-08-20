'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useModalForm } from '@/src/hooks'
import { useGetProjectPhaseQuery, useGetProjectPlanTreeQuery } from '@/src/store/features/ppm/projects-api'
import { useGetTaskStatusOptionsQuery } from '@/src/store/features/ppm/project-tasks-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { authenticatedFetch } from '@/src/services/clients'
import { toFormErrors } from '@/src/utils'
import { DatePicker, Form, InputNumber, Modal, Radio } from 'antd'
import dayjs from 'dayjs'
import { useEffect } from 'react'
import {
  findOwnChildrenSpan,
  getChildrenContainmentError,
  isShiftOnlyChange,
} from './project-parent-date-hint'

const { Item } = Form
const { RangePicker } = DatePicker
const { Group: RadioGroup } = Radio

export interface EditProjectPhaseFormProps {
  projectId: string
  phaseId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditPhaseFormValues {
  description: string
  statusId: number
  plannedRange: any[] | undefined
  progress: number
  assigneeIds: string[]
}

const EditProjectPhaseForm = ({
  projectId,
  phaseId,
  onFormComplete,
  onFormCancel,
}: EditProjectPhaseFormProps) => {
  const { data: phaseData, isLoading } = useGetProjectPhaseQuery(
    { projectId, phaseId },
    { skip: !projectId || !phaseId },
  )
  const { data: planTree } = useGetProjectPlanTreeQuery(projectId, { skip: !projectId })

  const { data: statusOptions = [] } = useGetTaskStatusOptionsQuery()
  const { data: employeeData } = useGetEmployeeOptionsQuery(true)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditPhaseFormValues>({
      onSubmit: async (values: EditPhaseFormValues, form: any): Promise<boolean> => {
          const patchOperations: Array<{
            op: 'replace'
            path: string
            value: unknown
          }> = []

          if (values.description !== phaseData?.description) {
            patchOperations.push({
              op: 'replace',
              path: '/Description',
              value: values.description,
            })
          }

          if (values.statusId !== phaseData?.status?.id) {
            patchOperations.push({
              op: 'replace',
              path: '/Status',
              value: values.statusId,
            })
          }

          const newStart =
            values.plannedRange?.[0]?.format('YYYY-MM-DD') ?? null
          const newEnd =
            values.plannedRange?.[1]?.format('YYYY-MM-DD') ?? null
          const oldStart = phaseData?.start
            ? dayjs(phaseData.start.toString()).format('YYYY-MM-DD')
            : null
          const oldEnd = phaseData?.end
            ? dayjs(phaseData.end.toString()).format('YYYY-MM-DD')
            : null

          if (newStart !== oldStart) {
            patchOperations.push({
              op: 'replace',
              path: '/PlannedStart',
              value: newStart,
            })
          }
          if (newEnd !== oldEnd) {
            patchOperations.push({
              op: 'replace',
              path: '/PlannedEnd',
              value: newEnd,
            })
          }

          if (values.progress !== phaseData?.progress) {
            patchOperations.push({
              op: 'replace',
              path: '/Progress',
              value: values.progress,
            })
          }

          const currentAssigneeIds =
            phaseData?.assignees?.map((a) => a.id).sort() ?? []
          const newAssigneeIds = [...(values.assigneeIds ?? [])].sort()
          if (
            JSON.stringify(currentAssigneeIds) !==
            JSON.stringify(newAssigneeIds)
          ) {
            patchOperations.push({
              op: 'replace',
              path: '/AssigneeIds',
              value: values.assigneeIds ?? [],
            })
          }

          if (patchOperations.length === 0) return true

          const response = await authenticatedFetch(
            `/api/ppm/projects/${projectId}/phases/${phaseId}`,
            {
              method: 'PATCH',
              headers: { 'Content-Type': 'application/json-patch+json' },
              body: JSON.stringify(patchOperations),
            },
          )

          if (!response.ok) {
            let errorData: any
            try {
              errorData = await response.json()
            } catch {
              errorData = { detail: await response.text() }
            }
            if (errorData?.errors) {
              const formErrors = toFormErrors(errorData.errors)
              form.setFields(formErrors)
            }
            return false
          }

          return true
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to update phase. Please try again.',
      permission: 'Permissions.Projects.Update',
    })

  useEffect(() => {
    if (phaseData) {
      const plannedRange =
        phaseData.start && phaseData.end
          ? [
              dayjs(phaseData.start.toString()),
              dayjs(phaseData.end.toString()),
            ]
          : undefined

      form.setFieldsValue({
        description: phaseData.description,
        statusId: phaseData.status?.id,
        plannedRange,
        progress: phaseData.progress ?? 0,
        assigneeIds: phaseData.assignees?.map((a) => a.id) ?? [],
      })
    }
  }, [phaseData, form])

  if (isLoading) {
    return null
  }

  return (
    <Modal
      title={`Edit Phase - ${phaseData?.name ?? ''}`}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid || isSaving }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={500}
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-project-phase-form"
      >
        <Item
          name="description"
          label="Description"
          rules={[
            { required: true, message: 'Description is required' },
            {
              max: 1024,
              message: 'Description cannot exceed 1024 characters',
            },
          ]}
        >
          <MarkdownEditor maxLength={1024} />
        </Item>

        <Item
          name="statusId"
          label="Status"
          rules={[{ required: true, message: 'Please select a status' }]}
        >
          <RadioGroup
            options={statusOptions}
            optionType="button"
            buttonStyle="solid"
          />
        </Item>

        <Item name="assigneeIds" label="Assignees">
          <EmployeeSelect
            employees={employeeData ?? []}
            allowMultiple={true}
            placeholder="Select Assignees"
          />
        </Item>

        <Item
          name="plannedRange"
          label="Planned Date Range"
          rules={[
            {
              validator: (_, value) => {
                const childrenSpan = findOwnChildrenSpan(planTree, phaseId)
                if (childrenSpan) {
                  if (!value || !value[0] || !value[1]) {
                    return Promise.reject(
                      new Error('Planned dates cannot be cleared when child items have dates.')
                    )
                  }
                  const start = value[0]
                  const end = value[1]
                  const originalStart = phaseData?.start ? dayjs(phaseData.start.toString()) : null
                  const originalEnd = phaseData?.end ? dayjs(phaseData.end.toString()) : null
                  const isShift = isShiftOnlyChange(originalStart, originalEnd, start, end)

                  if (!isShift) {
                    const containmentError = getChildrenContainmentError(childrenSpan, start, end)
                    if (containmentError) {
                      return Promise.reject(new Error(containmentError))
                    }
                  }
                }
                return Promise.resolve()
              }
            }
          ]}
        >
          <RangePicker style={{ width: '60%' }} format="MMM D, YYYY" />
        </Item>

        <Item name="progress" label="Progress %">
          <InputNumber min={0} max={100} style={{ width: '33%' }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditProjectPhaseForm
