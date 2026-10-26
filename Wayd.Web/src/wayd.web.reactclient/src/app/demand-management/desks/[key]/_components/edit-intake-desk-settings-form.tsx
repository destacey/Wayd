'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeDeskDto, IntakeDeskVisibility } from '@/src/services/wayd-api'
import {
  useGetIntakeDeskVisibilitiesQuery,
  useUpdateIntakeDeskMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Flex, Form, Input, Modal, Radio, Switch, Typography } from 'antd'
import { FC, useState } from 'react'
import { toUpdateRequest } from './desk-update'

const { Item } = Form
const { Text } = Typography

export interface EditIntakeDeskSettingsFormProps {
  desk: IntakeDeskDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditIntakeDeskSettingsFormValues {
  name: string
  description: string
  visibility: IntakeDeskVisibility
  isRequesterSelectable: boolean
  isOpen: boolean
  requesterMayAddParticipants?: boolean
}

/**
 * Changes a desk's name, description and settings. Offered where the desk's `canManage` says so; its
 * visibility only the Demand Management Admin changes, which the desk's `canAdminister` answers.
 */
const EditIntakeDeskSettingsForm: FC<EditIntakeDeskSettingsFormProps> = ({
  desk: openedDesk,
  onFormComplete,
  onFormCancel,
}) => {
  // The desk as it was when the form opened. The page refetches the desk while the form is open — a failed
  // save does too — and sending its newer revision with values edited from the older one would let a retry
  // overwrite whatever changed in between. Holding this snapshot keeps every retry refused until a reload.
  const [desk] = useState(openedDesk)
  const messageApi = useMessage()
  const [updateDesk] = useUpdateIntakeDeskMutation()
  const { data: visibilities } = useGetIntakeDeskVisibilitiesQuery()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditIntakeDeskSettingsFormValues>({
      onSubmit: async (values, form) => {
        try {
          await updateDesk(
            toUpdateRequest(desk, {
              name: values.name.trim(),
              description: values.description.trim(),
              visibility: values.visibility,
              isRequesterSelectable: values.isRequesterSelectable,
              isOpen: values.isOpen,
              // Asked only for a private desk; on any other it keeps what it has.
              requesterMayAddParticipants:
                values.requesterMayAddParticipants ??
                desk.requesterMayAddParticipants,
            }),
          ).unwrap()
          messageApi.success('Desk updated.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(apiError.detail ?? 'Failed to update the desk.')
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to update the desk.',
    })

  return (
    <Modal
      title="Edit Desk Settings"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={640}
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-intake-desk-settings-form"
        initialValues={{
          name: desk.name,
          description: desk.description,
          visibility: desk.visibility,
          isRequesterSelectable: desk.isRequesterSelectable,
          isOpen: desk.isOpen,
          requesterMayAddParticipants: desk.requesterMayAddParticipants,
        }}
      >
        <Item
          name="name"
          label="Name"
          extra="What its members and administrators call the desk."
          rules={[
            { required: true, whitespace: true, message: 'Name is required.' },
            { max: 128 },
          ]}
        >
          <Input maxLength={128} showCount />
        </Item>
        <Item
          name="description"
          label="What to bring here"
          extra="Requesters choose a desk by this, so describe the kind of ask it takes rather than the group behind it."
          rules={[
            {
              required: true,
              whitespace: true,
              message: 'Describe what this desk takes.',
            },
            { max: 1024 },
          ]}
        >
          <Input.TextArea
            autoSize={{ minRows: 2, maxRows: 4 }}
            maxLength={1024}
            showCount
          />
        </Item>
        <Item
          name="visibility"
          label="Who can see its requests"
          extra={
            desk.canAdminister
              ? 'Changing this changes who can read every request on the desk from now on.'
              : 'Only the Demand Management Admin can change who can see the requests on a desk.'
          }
          rules={[{ required: true, message: 'Choose a visibility.' }]}
        >
          <Radio.Group disabled={!desk.canAdminister}>
            <Flex vertical gap={8}>
              {(visibilities ?? []).map((v) => (
                <Radio key={v.code} value={v.code}>
                  <Flex vertical>
                    <Text>{v.name}</Text>
                    {v.description && (
                      <Text type="secondary" style={{ fontSize: 12 }}>
                        {v.description}
                      </Text>
                    )}
                  </Flex>
                </Radio>
              ))}
            </Flex>
          </Radio.Group>
        </Item>
        <Item
          noStyle
          shouldUpdate={(previous, current) =>
            previous.visibility !== current.visibility
          }
        >
          {({ getFieldValue }) =>
            getFieldValue('visibility') === IntakeDeskVisibility.Private && (
              <Item
                name="requesterMayAddParticipants"
                label="Requesters may add participants"
                valuePropName="checked"
                extra="Agents can always add people. A private desk keeps its requests from the company, not from the person asking."
              >
                <Switch />
              </Item>
            )
          }
        </Item>
        <Item
          name="isRequesterSelectable"
          label="Requesters can choose it"
          valuePropName="checked"
          extra="Turn off for a desk agents route requests to, but requesters never pick."
        >
          <Switch />
        </Item>
        <Item
          name="isOpen"
          label="Open to new requests"
          valuePropName="checked"
          extra="A closed desk keeps the requests already on it. Close a desk instead of deleting it."
        >
          <Switch />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditIntakeDeskSettingsForm
