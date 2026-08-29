'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { UpdateWorkTypeRequest } from '@/src/services/wayd-api'
import {
  useGetWorkTypeQuery,
  useUpdateWorkTypeMutation,
} from '@/src/store/features/work-management/work-type-api'
import { useGetWorkTypeLevelOptionsQuery } from '@/src/store/features/work-management/work-type-level-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Descriptions, Form, Input, Modal, Select } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input
const { Item: DescriptionItem } = Descriptions

export interface EditWorkTypeFormProps {
  workTypeId: number
  onFormSave: () => void
  onFormCancel: () => void
}

interface EditWorkTypeFormValues {
  description?: string
  levelId: number
}

const mapToRequestValues = (
  workTypeId: number,
  values: EditWorkTypeFormValues,
): UpdateWorkTypeRequest => {
  return {
    id: workTypeId,
    description: values.description,
    levelId: values.levelId,
  }
}

const EditWorkTypeForm = ({
  workTypeId,
  onFormSave,
  onFormCancel,
}: EditWorkTypeFormProps) => {
  const messageApi = useMessage()

  const { data: workTypeData } = useGetWorkTypeQuery(workTypeId)
  // TODO: why do I have to pass null here?
  const { data: workTypeLevelOptions } = useGetWorkTypeLevelOptionsQuery(null)
  const [updateWorkTypeMutation] = useUpdateWorkTypeMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditWorkTypeFormValues>({
      onSubmit: async (values: EditWorkTypeFormValues, form) => {
          try {
            const request = mapToRequestValues(workTypeId, values)
            const response = await updateWorkTypeMutation(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Successfully updated work type.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                'An unexpected error occurred while creating the work type.',
              )
              console.error('handleSave error', error)
            }
            return false
          }
        },
      onComplete: onFormSave,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while updating the work type.',
      permission: 'Permissions.WorkTypes.Update',
    })

  useEffect(() => {
    if (!workTypeData) return
    form.setFieldsValue({
      description: workTypeData.description,
      levelId: workTypeData.level.id,
    })
  }, [workTypeData, form])

  return (
    <Modal
      title="Edit Work Type"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      onCancel={handleCancel}
      confirmLoading={isSaving}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-work-type-form"
      >
        <Descriptions size="small" column={1}>
          <DescriptionItem label="Name">{workTypeData?.name}</DescriptionItem>
        </Descriptions>
        <Item label="Description" name="description">
          <TextArea
            autoSize={{ minRows: 4, maxRows: 10 }}
            showCount
            maxLength={1024}
          />
        </Item>
        <Item label="Level" name="levelId" rules={[{ required: true }]}>
          <Select options={workTypeLevelOptions} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditWorkTypeForm
