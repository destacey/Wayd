'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ObjectIdAndKey } from '@/src/services/wayd-api'
import {
  useAddIntakeRequestsToPokerSessionMutation,
  useGetIntakeRequestsQuery,
  useStartIntakeDeskPokerSessionMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal, Select, Typography } from 'antd'
import { FC, useRef } from 'react'

const { Item } = Form
const { Paragraph } = Typography

export interface DeskPokerSessionFormProps {
  desk: { id: string; name: string }
  /**
   * The session to add requests to. Without one, the form starts a new
   * session on the desk.
   */
  session?: { id: string }
  /** Requests chosen before the form opened, such as a selection on the requests page. */
  initialRequestIds?: string[]
  /** Requests that already have a round in the session, which are not offered again. */
  excludedRequestIds?: string[]
  /** Called with the new session when one was started, and with nothing when requests were added. */
  onFormComplete: (started?: ObjectIdAndKey) => void
  onFormCancel: () => void
}

interface DeskPokerSessionFormValues {
  name: string
  requestIds: string[]
}

/**
 * Starts a planning poker session on a desk with a round for each request
 * chosen, or adds rounds about more of the desk's requests to one of its
 * sessions. Offered to the desk's agents; the server decides.
 */
const DeskPokerSessionForm: FC<DeskPokerSessionFormProps> = ({
  desk,
  session,
  initialRequestIds,
  excludedRequestIds,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [startSession] = useStartIntakeDeskPokerSessionMutation()
  const [addRequests] = useAddIntakeRequestsToPokerSessionMutation()
  const { data: openRequests, isLoading } = useGetIntakeRequestsQuery({
    deskId: desk.id,
    openOnly: true,
  })

  const excluded = new Set(excludedRequestIds ?? [])
  const options = (openRequests ?? [])
    .filter((r) => !excluded.has(r.id))
    .map((r) => ({ value: r.id, label: `${r.key} · ${r.title}` }))

  // Read by onComplete, which may run from a later render than onSubmit did.
  const started = useRef<ObjectIdAndKey | undefined>(undefined)

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<DeskPokerSessionFormValues>({
      onSubmit: async (values, form) => {
        try {
          if (session) {
            await addRequests({
              deskId: desk.id,
              sessionId: session.id,
              requestIds: values.requestIds,
            }).unwrap()
            messageApi.success('Requests added to the session.')
          } else {
            started.current = await startSession({
              deskId: desk.id,
              name: values.name,
              requestIds: values.requestIds ?? [],
            }).unwrap()
            messageApi.success('Poker session started.')
          }
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                (session
                  ? 'Failed to add the requests.'
                  : 'Failed to start the session.'),
            )
          }
          return false
        }
      },
      onComplete: () => onFormComplete(started.current),
      onCancel: onFormCancel,
      errorMessage: session
        ? 'Failed to add the requests.'
        : 'Failed to start the session.',
    })

  return (
    <Modal
      title={session ? 'Add Requests to Session' : 'Start Poker Session'}
      open={isOpen}
      onOk={handleOk}
      okText={session ? 'Add' : 'Start'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        {session
          ? `Each request gets a round after the session's others.`
          : `A private session on ${desk.name}'s estimation scale, run by the desk's agents. Invite voters from the session once it has started.`}{' '}
        Everyone in the session sees each request&apos;s key, title and
        description, whatever the desk&apos;s visibility. Nothing is written to
        a request until its round is published.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="desk-poker-session-form"
        initialValues={{ requestIds: initialRequestIds ?? [] }}
      >
        {!session && (
          <Item
            name="name"
            label="Name"
            rules={[{ required: true, whitespace: true, max: 256 }]}
          >
            <Input maxLength={256} autoFocus />
          </Item>
        )}
        <Item
          name="requestIds"
          label="Requests"
          rules={session ? [{ required: true, type: 'array', min: 1 }] : []}
          extra={
            session
              ? undefined
              : 'Open requests on this desk. More can be added once the session has started.'
          }
        >
          <Select
            mode="multiple"
            aria-label="Requests"
            loading={isLoading}
            options={options}
            optionFilterProp="label"
            placeholder="Choose open requests"
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default DeskPokerSessionForm
