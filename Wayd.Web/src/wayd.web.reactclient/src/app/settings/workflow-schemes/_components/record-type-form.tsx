'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { RecordTypeDto } from '@/src/services/wayd-api'
import { useGetWorkflowOwnerTypesQuery } from '@/src/store/features/common/status-workflows-api'
import {
  useCreateRecordTypeMutation,
  useRenameRecordTypeMutation,
} from '@/src/store/features/common/workflow-schemes-api'
import { isApiError, toFormErrors, type ApiError } from '@/src/utils'
import { Form, Input, Modal, Select } from 'antd'
import { useEffect } from 'react'

const { Item } = Form

export interface RecordTypeFormProps {
  /** The record type to rename, or undefined to define a new one. */
  recordType?: Pick<RecordTypeDto, 'id' | 'name'>
  onFormComplete: () => void
  onFormCancel: () => void
}

interface RecordTypeFormValues {
  ownerType?: string
  name: string
}

/** Defines a record type, or renames an owned one. */
const RecordTypeForm = ({
  recordType,
  onFormComplete,
  onFormCancel,
}: RecordTypeFormProps) => {
  const messageApi = useMessage()
  const isRename = recordType !== undefined

  const [createRecordType] = useCreateRecordTypeMutation()
  const [renameRecordType] = useRenameRecordTypeMutation()

  const { data: ownerTypes, isLoading: ownerTypesLoading } =
    useGetWorkflowOwnerTypesQuery(undefined, { skip: isRename })

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<RecordTypeFormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = isRename
            ? await renameRecordType({
                id: recordType.id,
                request: { name: values.name },
              })
            : await createRecordType({
                ownerType: values.ownerType!,
                name: values.name,
              })
          if (response.error) throw response.error

          messageApi.success(
            isRename ? 'Record type renamed.' : 'Record type created.',
          )
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while saving the record type. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while saving the record type. Please try again.',
      permission: isRename
        ? 'Permissions.StatusWorkflows.Update'
        : 'Permissions.StatusWorkflows.Create',
    })

  useEffect(() => {
    if (recordType) form.setFieldsValue({ name: recordType.name })
  }, [recordType, form])

  return (
    <Modal
      title={isRename ? 'Rename Record Type' : 'Create Record Type'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={isRename ? 'Save' : 'Create'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="record-type-form">
        {!isRename && (
          <Item
            label="Owner Type"
            name="ownerType"
            tooltip="The kind of record this is a type of. Fixed once the record type is created."
            rules={[{ required: true, message: 'Owner Type is required' }]}
          >
            <Select
              loading={ownerTypesLoading}
              placeholder="Select an owner type"
              options={(ownerTypes ?? [])
                .filter((o) => o.hasRecordTypes)
                .map((o) => ({ value: o.key, label: o.displayName }))}
            />
          </Item>
        )}
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128 },
          ]}
        >
          <Input showCount maxLength={128} />
        </Item>
      </Form>
    </Modal>
  )
}

export default RecordTypeForm
