'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { UpdateWorkItemProjectRequest } from '@/src/services/wayd-api'
import { useGetProjectOptionsQuery } from '@/src/store/features/ppm/projects-api'
import {
  useGetWorkItemProjectInfoQuery,
  useUpdateWorkItemProjectMutation,
} from '@/src/store/features/work-management/workspace-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal, Select, Space, Typography } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { Text } = Typography

export interface EditWorkItemProjectFormProps {
  workItemId: string
  workItemKey: string
  workspaceId: string
  hasParent: boolean
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditWorkItemProjectFormValues {
  projectId?: string
}

const mapToRequestValues = (
  values: EditWorkItemProjectFormValues,
  workItemId: string,
): UpdateWorkItemProjectRequest => {
  return {
    workItemId: workItemId,
    projectId: values.projectId || undefined,
  }
}

const EditWorkItemProjectForm = (props: EditWorkItemProjectFormProps) => {
  const messageApi = useMessage()

  const [updateWorkItemProject] = useUpdateWorkItemProjectMutation()

  const {
    data: workItemProjectInfoData,
    isLoading,
    error,
  } = useGetWorkItemProjectInfoQuery({
    idOrKey: props.workspaceId,
    workItemKey: props.workItemKey,
  })

  const {
    data: projectOptions,
    isLoading: projectOptionsIsLoading,
    error: projectOptionsError,
  } = useGetProjectOptionsQuery()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditWorkItemProjectFormValues>({
      onSubmit: async (values: EditWorkItemProjectFormValues, form) => {
        try {
          const request = mapToRequestValues(values, props.workItemId)

          const response = await updateWorkItemProject({
            workspaceId: props.workspaceId,
            request: request,
            cacheKey: props.workItemKey,
          })

          if (response.error) {
            throw response.error
          }

          messageApi.success('Work item project updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the work item. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: props.onFormComplete,
      onCancel: props.onFormCancel,
      errorMessage:
        'An error occurred while updating the work item. Please try again.',
      permission: 'Permissions.Projects.ManageProjectWorkItems',
    })

  useEffect(() => {
    if (!workItemProjectInfoData || !isOpen) return

    const projectId =
      workItemProjectInfoData.projectSource === 'WorkItem'
        ? workItemProjectInfoData.project?.id
        : undefined

    form.setFieldsValue({ projectId })
  }, [workItemProjectInfoData, isOpen, form])

  useEffect(() => {
    if (error || projectOptionsError) {
      console.error(error ?? projectOptionsError)
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          (isApiError(projectOptionsError)
            ? projectOptionsError.detail
            : undefined) ??
          'An error occurred while loading form data. Please try again.',
      )
    }
  }, [error, messageApi, projectOptionsError])

  const hasParent = props.hasParent || workItemProjectInfoData?.hasParent === true

  const getProjectSourceText = () => {
    if (!workItemProjectInfoData) return undefined

    switch (workItemProjectInfoData.projectSource) {
      case 'WorkItem':
        return workItemProjectInfoData.parentProject
          ? `This work item is using its own project assignment instead of inheriting ${workItemProjectInfoData.parentProject.name} from its parent. Clear the field and save to inherit the parent project again.`
          : 'This work item is using its own project assignment. Clear the field and save to remove the project.'
      case 'Parent':
        return workItemProjectInfoData.parentProject
          ? `This work item is inheriting ${workItemProjectInfoData.parentProject.name} from its parent. Select a different project and save to override it on this work item.`
          : 'This work item is inheriting its project from the parent. Select a different project and save to override it on this work item.'
      default:
        return hasParent
          ? 'This work item and its parent do not currently have a project. Select a project and save to assign one directly to this work item.'
          : 'This work item is not associated with any project. Select a project and save to assign one directly.'
    }
  }

  const projectSourceText = getProjectSourceText()

  const projectSelectOptions =
    workItemProjectInfoData?.parentProject?.id && projectOptions
      ? projectOptions.filter(
          (option) => option.value !== workItemProjectInfoData.parentProject?.id,
        )
      : projectOptions

  const parentProjectName = workItemProjectInfoData?.parentProject?.name ?? 'No Project'

  return (
    <>
      <Modal
        title="Edit Project"
        open={isOpen}
        onOk={handleOk}
        okButtonProps={{ disabled: !isValid }}
        okText="Save"
        confirmLoading={isSaving}
        onCancel={handleCancel}
        keyboard={false}
        destroyOnHidden
      >
        <Space vertical>
          {projectSourceText && <Text italic>{projectSourceText}</Text>}
          {hasParent && (
            <Text>Parent Project: {parentProjectName}</Text>
          )}
          <Form
            form={form}
            size="small"
            layout="vertical"
            name="edit-workitem-project-form"
          >
            <Item name="projectId" label="Project">
              <Select
                allowClear
                options={projectSelectOptions ?? []}
                placeholder="Select Project"
                showSearch
                optionFilterProp="label"
                filterOption={(input, option) =>
                  (option?.label?.toLowerCase() ?? '').includes(
                    input.toLowerCase(),
                  )
                }
              />
            </Item>
          </Form>
        </Space>
      </Modal>
    </>
  )
}

export default EditWorkItemProjectForm
