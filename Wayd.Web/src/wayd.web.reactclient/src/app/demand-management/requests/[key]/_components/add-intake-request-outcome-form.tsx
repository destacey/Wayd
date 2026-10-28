'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  IntakeRequestDetailsDto,
  IntakeRequestOutcomeKind,
} from '@/src/services/wayd-api'
import { useAddIntakeRequestOutcomeMutation } from '@/src/store/features/demand-management/demand-management-api'
import { useGetProjectOptionsQuery } from '@/src/store/features/ppm/projects-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal, Radio, Select, Typography } from 'antd'
import { FC } from 'react'

const { Item } = Form
const { Paragraph } = Typography
const { TextArea } = Input

export interface AddIntakeRequestOutcomeFormProps {
  request: IntakeRequestDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddOutcomeFormValues {
  kind: IntakeRequestOutcomeKind
  projectId: string
  note?: string
}

/**
 * Records a project the request became. Offered only where the server's `canChangeOutcomes` says so. The
 * project list is the reader's own view of PPM, so a project they cannot see is not offered.
 */
const AddIntakeRequestOutcomeForm: FC<AddIntakeRequestOutcomeFormProps> = ({
  request,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [addOutcome] = useAddIntakeRequestOutcomeMutation()
  const {
    data: projects,
    isLoading: projectsLoading,
    isError: projectsFailed,
  } = useGetProjectOptionsQuery()

  const alreadyLinked = new Set(request.outcomes.map((o) => o.targetId))
  const projectOptions = (projects ?? []).filter(
    (p) => !alreadyLinked.has(p.value as string),
  )

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddOutcomeFormValues>({
      onSubmit: async (values, form) => {
        try {
          await addOutcome({
            requestId: request.summary.id,
            request: {
              kind: values.kind,
              projectId: values.projectId,
              note: values.note,
            },
          }).unwrap()
          messageApi.success('Outcome added.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(apiError.detail ?? 'Failed to add the outcome.')
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to add the outcome.',
    })

  const kind = Form.useWatch('kind', form)
  const noteRequired = kind === IntakeRequestOutcomeKind.ExistingProjectScope

  return (
    <Modal
      title="Add Outcome"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Add"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        Record what this request became. The project itself is not changed.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="add-intake-request-outcome-form"
        initialValues={{ kind: IntakeRequestOutcomeKind.NewProject }}
      >
        <Item name="kind" label="Outcome" rules={[{ required: true }]}>
          <Radio.Group
            options={[
              {
                label: 'New project',
                value: IntakeRequestOutcomeKind.NewProject,
              },
              {
                label: 'Scope added to an existing project',
                value: IntakeRequestOutcomeKind.ExistingProjectScope,
              },
            ]}
          />
        </Item>
        <Item
          name="projectId"
          label="Project"
          rules={[{ required: true, message: 'Choose a project.' }]}
          validateStatus={projectsFailed ? 'error' : undefined}
          help={
            projectsFailed ? 'The projects could not be loaded.' : undefined
          }
        >
          <Select
            showSearch={{ optionFilterProp: 'label' }}
            placeholder="Select a project"
            loading={projectsLoading}
            options={projectOptions}
          />
        </Item>
        <Item
          name="note"
          label="Note"
          required={noteRequired}
          dependencies={['kind']}
          rules={[
            ({ getFieldValue }) => ({
              required:
                getFieldValue('kind') ===
                IntakeRequestOutcomeKind.ExistingProjectScope,
              whitespace: true,
              message: 'Say what the project absorbed from this request.',
            }),
            { max: 1024 },
          ]}
          extra={
            noteRequired
              ? 'What the project took on from this request.'
              : undefined
          }
        >
          <TextArea autoSize={{ minRows: 2, maxRows: 6 }} maxLength={1024} />
        </Item>
      </Form>
    </Modal>
  )
}

export default AddIntakeRequestOutcomeForm
