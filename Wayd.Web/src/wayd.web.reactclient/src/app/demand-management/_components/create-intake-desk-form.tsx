'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeDeskRole, IntakeDeskVisibility } from '@/src/services/wayd-api'
import {
  useCreateIntakeDeskMutation,
  useGetIntakeDeskVisibilitiesQuery,
  useGetIntakeEmployeeOptionsQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Flex, Form, Input, Modal, Radio, Switch, Typography } from 'antd'
import { FC } from 'react'

const { Item } = Form
const { Text } = Typography

export interface CreateIntakeDeskFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateIntakeDeskFormValues {
  name: string
  description: string
  visibility: IntakeDeskVisibility
  isRequesterSelectable: boolean
  isOpen: boolean
  requesterMayAddParticipants: boolean
  adminIds: string[]
  agentIds: string[]
}

/**
 * Creates a desk with its first members. Only the Demand Management Admin creates desks; each is then run
 * from its own page.
 */
const CreateIntakeDeskForm: FC<CreateIntakeDeskFormProps> = ({
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [createDesk] = useCreateIntakeDeskMutation()
  const { data: visibilities } = useGetIntakeDeskVisibilitiesQuery()
  const { data: employees } = useGetIntakeEmployeeOptionsQuery()
  const employeeOptions = (employees ?? []).map((e) => ({
    label: e.name,
    value: e.id,
  }))

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateIntakeDeskFormValues>({
      onSubmit: async (values, form) => {
        try {
          await createDesk({
            name: values.name.trim(),
            description: values.description.trim(),
            visibility: values.visibility,
            isRequesterSelectable: values.isRequesterSelectable,
            isOpen: values.isOpen,
            // Asked only for a private desk, so on any other the field is unmounted and the form omits
            // it; send the API's own default rather than nothing.
            requesterMayAddParticipants:
              values.requesterMayAddParticipants ?? true,
            members: [
              ...(values.adminIds ?? []).map((employeeId) => ({
                employeeId,
                role: IntakeDeskRole.Admin,
              })),
              ...(values.agentIds ?? []).map((employeeId) => ({
                employeeId,
                role: IntakeDeskRole.Agent,
              })),
            ],
          }).unwrap()
          messageApi.success('Desk created.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(apiError.detail ?? 'Failed to create the desk.')
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to create the desk.',
      permission: 'Permissions.IntakeRequests.Administer',
    })

  return (
    <Modal
      title="Create Desk"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
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
        name="create-intake-desk-form"
        initialValues={{
          isRequesterSelectable: true,
          isOpen: true,
          requesterMayAddParticipants: true,
          adminIds: [],
          agentIds: [],
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
          rules={[{ required: true, message: 'Choose a visibility.' }]}
        >
          <Radio.Group>
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
        >
          <Switch />
        </Item>
        <Item
          name="adminIds"
          label="Desk Admins"
          extra="Run the desk: change its details and settings, name its agents, and delete its requests. They also do everything an agent does."
        >
          <EmployeeSelect allowMultiple employees={employeeOptions} />
        </Item>
        <Item
          name="agentIds"
          label="Agents"
          extra="Work the desk's requests. A desk with no members has nobody to look at what arrives."
          dependencies={['adminIds']}
          rules={[
            ({ getFieldValue }) => ({
              validator: (_, agentIds: string[] | undefined) => {
                const adminIds: string[] = getFieldValue('adminIds') ?? []
                return (agentIds ?? []).some((id) => adminIds.includes(id))
                  ? Promise.reject(
                      new Error(
                        'Someone holds one role on a desk: a Desk Admin already does everything an agent does.',
                      ),
                    )
                  : Promise.resolve()
              },
            }),
          ]}
        >
          <EmployeeSelect allowMultiple employees={employeeOptions} />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateIntakeDeskForm
