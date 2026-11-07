'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProjectDeliverableDto } from '@/src/services/wayd-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import {
  useCreateProjectDeliverableMutation,
  useUpdateProjectDeliverableMutation,
} from '@/src/store/features/ppm/project-deliverables-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { parseCalendarDate, toCalendarDate } from '@/src/utils/calendar-date'
import { DatePicker, Form, Input, Modal } from 'antd'
import { Dayjs } from 'dayjs'
import { deliverableErrorMessage } from './project-deliverable-errors'

const { Item } = Form
const { TextArea } = Input

/** The server's limit on the stored markdown of the description and acceptance criteria. */
const TEXT_MAX_LENGTH = 4096

export interface ProjectDeliverableFormProps {
  projectId: string
  /** The project's revision when the form opened; a project changed since refuses the save. */
  projectRevision: number
  /** The deliverable to edit; omit to add a new one. */
  deliverable?: ProjectDeliverableDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ProjectDeliverableFormValues {
  name: string
  description?: string
  acceptanceCriteria?: string
  ownerId?: string
  targetDate?: Dayjs
}

/**
 * Adds a deliverable to a project, or changes one's descriptive fields. A new
 * deliverable starts Not Started and goes last; its status changes through its
 * own form, so this one never touches it.
 */
const ProjectDeliverableForm = ({
  projectId,
  projectRevision,
  deliverable,
  onFormComplete,
  onFormCancel,
}: ProjectDeliverableFormProps) => {
  const messageApi = useMessage()
  const [createDeliverable] = useCreateProjectDeliverableMutation()
  const [updateDeliverable] = useUpdateProjectDeliverableMutation()
  const isEdit = !!deliverable
  // Editing includes inactive people, so an owner who has since left still shows by name.
  const { data: employees } = useGetEmployeeOptionsQuery(isEdit)
  const failure = isEdit
    ? 'Failed to update the deliverable.'
    : 'Failed to add the deliverable.'

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ProjectDeliverableFormValues>({
      permission: 'Permissions.Projects.Update',
      onSubmit: async (values, form) => {
        const request = {
          name: values.name.trim(),
          description: values.description?.trim() || undefined,
          acceptanceCriteria: values.acceptanceCriteria?.trim() || undefined,
          ownerId: values.ownerId || undefined,
          targetDate: values.targetDate
            ? toCalendarDate(values.targetDate)
            : undefined,
          expectedRevision: projectRevision,
        }
        try {
          if (deliverable) {
            await updateDeliverable({
              projectId,
              deliverableId: deliverable.id,
              request,
            }).unwrap()
            messageApi.success('Deliverable updated.')
          } else {
            await createDeliverable({ projectId, request }).unwrap()
            messageApi.success('Deliverable added.')
          }
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(deliverableErrorMessage(apiError, failure))
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: failure,
    })

  return (
    <Modal
      title={isEdit ? 'Edit Deliverable' : 'Add Deliverable'}
      open={isOpen}
      width="min(90vw, 560px)"
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={isEdit ? 'Save' : 'Add'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="project-deliverable-form"
        initialValues={{
          name: deliverable?.name,
          description: deliverable?.description,
          acceptanceCriteria: deliverable?.acceptanceCriteria,
          ownerId: deliverable?.owner?.id,
          targetDate: deliverable?.targetDate
            ? parseCalendarDate(deliverable.targetDate)
            : undefined,
        }}
      >
        <Item
          name="name"
          label="Name"
          rules={[
            { required: true, whitespace: true, message: 'Name is required.' },
            { max: 256 },
          ]}
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={256}
          />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[{ max: TEXT_MAX_LENGTH }]}
        >
          <MarkdownEditor maxLength={TEXT_MAX_LENGTH} rows={4} />
        </Item>
        <Item
          name="acceptanceCriteria"
          label="Acceptance Criteria"
          tooltip="How the project will know this is done. It is accepted against these."
          rules={[{ max: TEXT_MAX_LENGTH }]}
        >
          <MarkdownEditor maxLength={TEXT_MAX_LENGTH} rows={4} />
        </Item>
        <Item name="ownerId" label="Owner">
          <EmployeeSelect
            employees={employees ?? []}
            placeholder="Select the accountable person"
          />
        </Item>
        <Item name="targetDate" label="Target Date">
          <DatePicker format="MMM D, YYYY" />
        </Item>
      </Form>
    </Modal>
  )
}

export default ProjectDeliverableForm
