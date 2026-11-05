'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  IntakeRequestDetailsDto,
  IntakeRequestStatusOptionDto,
} from '@/src/services/wayd-api'
import {
  useChangeIntakeRequestStatusMutation,
  useGetIntakeRequestStatusOptionsQuery,
  useGetIntakeRequestsQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Alert, Form, Input, Modal, Select } from 'antd'
import { FC } from 'react'

const { Item } = Form
const { TextArea } = Input

/**
 * The status alias meaning Waiting on Requester. A value of the server's `IntakeRequestStatusAlias`, which the
 * generated client carries only as a number; matched by meaning so a desk that renames the status still has it.
 */
export const WAITING_ON_REQUESTER = 2

/** Whether a move to the status is asking the requester something, so the form asks for the question. */
export const asksTheRequester = (status: { alias: number } | undefined) =>
  status?.alias === WAITING_ON_REQUESTER

export interface ChangeIntakeRequestStatusFormProps {
  request: IntakeRequestDetailsDto
  /** The status to start on, when the move was picked before the form opened. */
  initialStatusId?: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeStatusFormValues {
  statusId: string
  reason?: string
  duplicateOfId?: string
  question?: string
}

/**
 * Moves a request to another status of its desk's workflow. Offered where the server's `canChangeStatus`
 * says so. The statuses come from the desk's workflow, in its order, and each says what the move needs: a
 * reason the requester reads, and for a duplicate the request it gives way to. The reason and the surviving
 * request appear only once a status that needs them is chosen.
 *
 * Moving to Waiting on Requester asks for the question instead of a reason. It is posted as a reply in the
 * request's discussion, saved with the move, so the requester can answer it there and the discussion shows the
 * move on the question.
 */
const ChangeIntakeRequestStatusForm: FC<ChangeIntakeRequestStatusFormProps> = ({
  request,
  initialStatusId,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [changeStatus] = useChangeIntakeRequestStatusMutation()
  const {
    data: options,
    isLoading,
    isError,
  } = useGetIntakeRequestStatusOptionsQuery(request.summary.id)
  const { data: requests } = useGetIntakeRequestsQuery()

  const option = (statusId: string | undefined) =>
    options?.find((o) => o.id === statusId)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeStatusFormValues>({
      onSubmit: async (values, form) => {
        const chosen = option(values.statusId)
        const question = values.question?.trim()
        try {
          await changeStatus({
            requestId: request.summary.id,
            statusId: values.statusId,
            reason: values.reason?.trim() || undefined,
            duplicateOfId: chosen?.needsSurvivingRequest
              ? values.duplicateOfId
              : undefined,
            reply:
              asksTheRequester(chosen) && question
                ? { body: question }
                : undefined,
          }).unwrap()
          messageApi.success('Status changed.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(apiError.detail ?? 'Failed to change the status.')
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to change the status.',
    })

  // Any other request the reader can see may be the one this duplicates; the server refuses one that is
  // itself a duplicate, with the reason.
  const survivors = (requests ?? []).filter((r) => r.id !== request.summary.id)

  const renderNeeds = (chosen: IntakeRequestStatusOptionDto | undefined) => (
    <>
      {chosen?.needsSurvivingRequest && (
        <Item
          name="duplicateOfId"
          label="Duplicate Of"
          extra="The request this one gives way to. Whoever can see it is taken there in place of this one; anyone who cannot sees this request marked Duplicate, with your reason, and no link."
          rules={[
            { required: true, message: 'Choose the request this duplicates.' },
          ]}
        >
          <Select
            placeholder="Select a request"
            showSearch={{ optionFilterProp: 'label' }}
            options={survivors.map((r) => ({
              value: r.id,
              label: `${r.key} · ${r.title}`,
            }))}
          />
        </Item>
      )}
      {asksTheRequester(chosen) && !chosen?.needsReason ? (
        <Item
          name="question"
          label="Question for the Requester"
          extra="Posted in the request's discussion with the move, where the requester can answer it. Leave it empty to move the request without asking anything."
          rules={[
            {
              max: 10000,
              message: 'Keep the question under 10,000 characters.',
            },
          ]}
        >
          <TextArea autoSize={{ minRows: 3 }} />
        </Item>
      ) : (
        /* Revalidated when the status changes, so an error for a status that needed a reason clears with it. */
        <Item
          name="reason"
          label="Reason"
          dependencies={['statusId']}
          extra={
            chosen?.needsReason
              ? 'Required for this status. The requester can read it.'
              : 'The requester can read it.'
          }
          rules={[
            {
              required: !!chosen?.needsReason,
              whitespace: true,
              message: 'A reason is required for this status.',
            },
            { max: 1024, message: 'Keep the reason under 1024 characters.' },
          ]}
        >
          <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
        </Item>
      )}
    </>
  )

  return (
    <Modal
      title="Change Status"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Change"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 12 }}
          title="The statuses this request can move to could not be loaded."
        />
      )}
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-intake-request-status-form"
        initialValues={{ statusId: initialStatusId }}
      >
        <Item
          name="statusId"
          label="Status"
          extra={`Currently ${request.summary.status.name}.`}
          rules={[{ required: true, message: 'Choose a status.' }]}
        >
          <Select
            placeholder="Select a status"
            loading={isLoading}
            options={(options ?? []).map((o) => ({
              value: o.id,
              label: o.name,
            }))}
          />
        </Item>
        <Item
          noStyle
          shouldUpdate={(previous, current) =>
            previous.statusId !== current.statusId
          }
        >
          {({ getFieldValue }) =>
            renderNeeds(option(getFieldValue('statusId')))
          }
        </Item>
      </Form>
    </Modal>
  )
}

export default ChangeIntakeRequestStatusForm
