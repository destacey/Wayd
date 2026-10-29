'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeDeskDto, IntakeDeskVisibility } from '@/src/services/wayd-api'
import {
  useGetIntakeChannelsQuery,
  useGetIntakeRequestTagCategoriesQuery,
  useGetIntakeDeskVisibilitiesQuery,
  useUpdateIntakeDeskMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import {
  Checkbox,
  Flex,
  Form,
  Input,
  Modal,
  Radio,
  Switch,
  Typography,
} from 'antd'
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
  offersAllChannels: boolean
  channelIds?: string[]
  offersAllTagCategories: boolean
  tagCategoryIds?: string[]
  allowsFreeTags: boolean
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
  const { data: channels } = useGetIntakeChannelsQuery({
    includeArchived: true,
  })
  // Active channels, plus any archived one the desk already chose: it may stay
  // chosen, and is offered again if restored, but cannot be newly chosen.
  const channelOptions = (channels ?? [])
    .filter((c) => c.isActive || desk.channelIds.includes(c.id))
    .map((c) => ({
      value: c.id,
      label: c.isActive ? c.name : `${c.name} (Archived)`,
    }))
  const { data: tagCategories } = useGetIntakeRequestTagCategoriesQuery({
    includeInactive: true,
  })
  // Active axes, plus any inactive one the desk already chose: it may stay
  // chosen, and is offered again if activated, but cannot be newly chosen.
  const tagCategoryOptions = (tagCategories ?? [])
    .filter((c) => c.isActive || desk.tagCategoryIds.includes(c.id))
    .map((c) => ({
      value: c.id,
      label: c.isActive ? c.name : `${c.name} (Inactive)`,
    }))

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
              offersAllChannels: values.offersAllChannels,
              // Asked only when the desk chooses its channels.
              channelIds: values.offersAllChannels
                ? []
                : (values.channelIds ?? desk.channelIds),
              offersAllTagCategories: values.offersAllTagCategories,
              // Asked only when the desk chooses its axes.
              tagCategoryIds: values.offersAllTagCategories
                ? []
                : (values.tagCategoryIds ?? desk.tagCategoryIds),
              allowsFreeTags: values.allowsFreeTags,
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
          offersAllChannels: desk.offersAllChannels,
          channelIds: desk.channelIds,
          offersAllTagCategories: desk.offersAllTagCategories,
          tagCategoryIds: desk.tagCategoryIds,
          allowsFreeTags: desk.allowsFreeTags,
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
          name="offersAllChannels"
          label="Channels offered"
          extra="Where a request came from, as requesters choose it when submitting to this desk."
        >
          <Radio.Group>
            <Flex vertical gap={8}>
              <Radio value={true}>
                Every channel, including ones added later
              </Radio>
              <Radio value={false}>Only the channels chosen here</Radio>
            </Flex>
          </Radio.Group>
        </Item>
        <Item
          noStyle
          shouldUpdate={(previous, current) =>
            previous.offersAllChannels !== current.offersAllChannels
          }
        >
          {({ getFieldValue }) =>
            getFieldValue('offersAllChannels') === false && (
              <Item
                name="channelIds"
                label="Channels this desk offers"
                extra="Choose none and requesters are not asked where a request came from. A request moved here keeps the channel it has."
              >
                <Checkbox.Group options={channelOptions} />
              </Item>
            )
          }
        </Item>
        <Item
          name="offersAllTagCategories"
          label="Tag axes offered"
          extra="The axes this desk's agents tag requests along."
        >
          <Radio.Group>
            <Flex vertical gap={8}>
              <Radio value={true}>
                Every tag axis, including ones added later
              </Radio>
              <Radio value={false}>Only the tag axes chosen here</Radio>
            </Flex>
          </Radio.Group>
        </Item>
        <Item
          noStyle
          shouldUpdate={(previous, current) =>
            previous.offersAllTagCategories !== current.offersAllTagCategories
          }
        >
          {({ getFieldValue }) =>
            getFieldValue('offersAllTagCategories') === false && (
              <Item
                name="tagCategoryIds"
                label="Tag axes this desk uses"
                extra="Choose none and the desk's requests are not tagged. A request moved here keeps the tags it has."
              >
                <Checkbox.Group options={tagCategoryOptions} />
              </Item>
            )
          }
        </Item>
        <Item
          name="allowsFreeTags"
          label="Allow free tags"
          valuePropName="checked"
          extra="Lets agents add their own tags, outside the tag axes, by typing a name. Turning it off keeps the free tags requests already carry."
        >
          <Switch />
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
