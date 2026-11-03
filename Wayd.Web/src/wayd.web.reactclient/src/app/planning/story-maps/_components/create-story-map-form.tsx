'use client'

import { Form, Input, Modal } from 'antd'
import {
  CreateStoryMapRequest,
  StoryMapEditAccess,
  Visibility,
} from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { useMessage } from '@/src/components/contexts/messaging'
import useAuth from '@/src/components/contexts/auth'
import { useCreateStoryMapMutation } from '@/src/store/features/planning/story-maps-api'
import { useModalForm } from '@/src/hooks'
import StoryMapAccessFields, {
  type StoryMapAccessValues,
} from './story-map-access-fields'

const { Item } = Form
const { TextArea } = Input

export interface CreateStoryMapFormProps {
  onFormCreate: () => void
  onFormCancel: () => void
}

interface CreateStoryMapFormValues extends StoryMapAccessValues {
  name: string
  description?: string
}

const CreateStoryMapForm = ({
  onFormCreate,
  onFormCancel,
}: CreateStoryMapFormProps) => {
  const messageApi = useMessage()
  const { user } = useAuth()
  // The creator is always an owner; the page offers Create only to accounts linked to an employee.
  const currentEmployeeId = user?.employeeId ?? undefined

  const [createStoryMap] = useCreateStoryMapMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateStoryMapFormValues>({
      onSubmit: async (values: CreateStoryMapFormValues, form) => {
        try {
          const request: CreateStoryMapRequest = {
            name: values.name,
            description: values.description,
            visibility: values.visibility,
            editAccess:
              values.editAccess ?? StoryMapEditAccess.AnyoneWhoCanView,
            ownerIds: (values.ownerIds ?? []).filter(
              (id) => id !== currentEmployeeId,
            ),
            editorIds: values.editorIds ?? [],
            editorTeamIds: values.editorTeamIds ?? [],
          }
          const response = await createStoryMap(request)
          if (response.error) {
            throw response.error
          }
          messageApi.success('Successfully created story map.')
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
                'An error occurred while creating the story map. Please try again.',
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormCreate,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the story map. Please try again.',
      permission: 'Permissions.StoryMaps.Create',
    })

  return (
    <Modal
      title="Create Story Map"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-story-map-form"
        initialValues={{
          visibility: Visibility.Public,
          editAccess: StoryMapEditAccess.AnyoneWhoCanView,
          ownerIds: currentEmployeeId ? [currentEmployeeId] : [],
          editorIds: [],
          editorTeamIds: [],
        }}
      >
        <Item label="Name" name="name" rules={[{ required: true }]}>
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item label="Description" name="description">
          <TextArea
            autoSize={{ minRows: 4, maxRows: 8 }}
            showCount
            maxLength={2048}
          />
        </Item>
        <StoryMapAccessFields requiredOwnerId={currentEmployeeId} />
      </Form>
    </Modal>
  )
}

export default CreateStoryMapForm
