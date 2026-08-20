'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useModalForm } from '@/src/hooks'
import { useGetProjectStageQuery, useGetProjectPlanTreeQuery } from '@/src/store/features/ppm/projects-api'
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

export interface EditProjectStageFormProps {
  projectId: string
  stageId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditStageFormValues {
  description: string
  statusId: number
  plannedRange: any[] | undefined
  progress: number
  assigneeIds: string[]
}

const EditProjectStageForm = ({
  projectId,
  stageId,
  onFormComplete,
  onFormCancel,
}: EditProjectStageFormProps) => {
  const { data: stageData, isLoading } = useGetProjectStageQuery(
    { projectId, stageId },
    { skip: !projectId || !stageId },
  )
  const { data: planTree } = useGetProjectPlanTreeQuery(projectId, { skip: !projectId })

  const { data: statusOptions = [] } = useGetTaskStatusOptionsQuery()
  const { data: employeeData } = useGetEmployeeOptionsQuery(true)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditStageFormValues>({
      onSubmit: async (values: EditStageFormValues, form: any): Promise<boolean> => {
          const patchOperations: Array<{
            op: 'replace'
            path: string
            value: unknown
          }> = []

          if (values.description !== stageData?.description) {
            patchOperations.push({
              op: 'replace',
              path: '/Description',
              value: values.description,
            })
          }

          if (values.statusId !== stageData?.status?.id) {
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
          const oldStart = stageData?.start
            ? dayjs(stageData.start.toString()).format('YYYY-MM-DD')
            : null
          const oldEnd = stageData?.end
            ? dayjs(stageData.end.toString()).format('YYYY-MM-DD')
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

          if (values.progress !== stageData?.progress) {
            patchOperations.push({
              op: 'replace',
              path: '/Progress',
              value: values.progress,
            })
          }

          const currentAssigneeIds =
            stageData?.assignees?.map((a) => a.id).sort() ?? []
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
            `/api/ppm/projects/${projectId}/stages/${stageId}`,
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
      errorMessage: 'Failed to update stage. Please try again.',
      permission: 'Permissions.Projects.Update',
    })

  useEffect(() => {
    if (stageData) {
      const plannedRange =
        stageData.start && stageData.end
          ? [
              dayjs(stageData.start.toString()),
              dayjs(stageData.end.toString()),
            ]
          : undefined

      form.setFieldsValue({
        description: stageData.description,
        statusId: stageData.status?.id,
        plannedRange,
        progress: stageData.progress ?? 0,
        assigneeIds: stageData.assignees?.map((a) => a.id) ?? [],
      })
    }
  }, [stageData, form])

  if (isLoading) {
    return null
  }

  return (
    <Modal
      title={`Edit Stage - ${stageData?.name ?? ''}`}
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
        name="edit-project-stage-form"
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
                const childrenSpan = findOwnChildrenSpan(planTree, stageId)
                if (childrenSpan) {
                  if (!value || !value[0] || !value[1]) {
                    return Promise.reject(
                      new Error('Planned dates cannot be cleared when child items have dates.')
                    )
                  }
                  const start = value[0]
                  const end = value[1]
                  const originalStart = stageData?.start ? dayjs(stageData.start.toString()) : null
                  const originalEnd = stageData?.end ? dayjs(stageData.end.toString()) : null
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

export default EditProjectStageForm
