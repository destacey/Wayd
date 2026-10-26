'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  IntakeDeskDto,
  IntakeDeskVisibility,
  IntakeRequestDetailsDto,
} from '@/src/services/wayd-api'
import {
  useGetIntakeDesksQuery,
  useGetIntakeRequestArrivalStatusesQuery,
  useRerouteIntakeRequestMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Alert, Checkbox, Form, Modal, Select, Typography } from 'antd'
import type { FormInstance } from 'antd'
import { FC, useEffect } from 'react'

const { Item } = Form
const { Paragraph } = Typography

const privacy: Record<IntakeDeskVisibility, number> = {
  [IntakeDeskVisibility.Open]: 1,
  [IntakeDeskVisibility.SummaryOnly]: 2,
  [IntakeDeskVisibility.Private]: 3,
}

/**
 * What a move from one desk to another makes visible, and to whom, or null when it makes nothing more
 * visible. Readers are granted by the request's current desk alone, so the request's history comes with
 * what it shows — earlier versions included — and the notice says so. The server refuses the move unless
 * it is confirmed.
 */
export const wideningNotice = (
  from: IntakeDeskVisibility,
  to: IntakeDeskVisibility,
): string | null => {
  if (privacy[to] >= privacy[from]) return null

  const prefix = 'Everyone who can view requests will see'
  const summary =
    'its title, requester, channel, need-by date and assignee, with earlier titles, past assignees and every desk it has been on, including this one'
  const substance =
    'its description, expected benefits and participants, who submitted it and where it came from, earlier versions of the description, and who made each change in its history'
  if (from === IntakeDeskVisibility.Private) {
    return to === IntakeDeskVisibility.Open
      ? `${prefix} the whole request: ${summary}; and ${substance}.`
      : `${prefix} ${summary}.`
  }
  return `${prefix} ${substance}.`
}

export interface RerouteIntakeRequestFormProps {
  request: IntakeRequestDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface RerouteFormValues {
  deskId: string
  statusId?: string
  confirmWiderVisibility?: boolean
}

interface ArrivalStatusFieldProps {
  requestId: string
  deskId: string
  form: FormInstance<RerouteFormValues>
}

/**
 * The status the request arrives in on the chosen desk: one of that desk's open statuses that needs no
 * reason, starting at the one the server matches to the request's own. Required when nothing matches.
 */
const ArrivalStatusField: FC<ArrivalStatusFieldProps> = ({
  requestId,
  deskId,
  form,
}) => {
  const { data: arrival, isFetching } = useGetIntakeRequestArrivalStatusesQuery(
    { requestId, deskId },
  )

  // Prefill once the destination's statuses arrive, without overwriting a choice already made.
  useEffect(() => {
    if (arrival?.defaultStatusId && !form.getFieldValue('statusId'))
      form.setFieldValue('statusId', arrival.defaultStatusId)
  }, [arrival, form])

  return (
    <Item
      name="statusId"
      label="Arrives As"
      extra="What the request is on that desk. Deciding it — declining, deferring and the rest — is that desk's call."
      rules={[{ required: true, message: 'Choose the status it arrives in.' }]}
    >
      <Select
        placeholder="Select a status"
        loading={isFetching}
        options={(arrival?.statuses ?? []).map((s) => ({
          label: s.name,
          value: s.id,
        }))}
      />
    </Item>
  )
}

/**
 * Moves the request to another desk. Offered only where the server's `canReroute` says so. A move that
 * makes the request visible to more people asks the mover to confirm, naming what becomes visible to whom;
 * a move to a more private desk is silent.
 */
const RerouteIntakeRequestForm: FC<RerouteIntakeRequestFormProps> = ({
  request,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [reroute] = useRerouteIntakeRequestMutation()
  const { data: desks } = useGetIntakeDesksQuery()

  const current = desks?.find((d) => d.id === request.summary.desk.id)
  // A desk closed to new requests cannot receive one; one requesters cannot pick still can.
  const destinations = (desks ?? []).filter(
    (d) => d.id !== request.summary.desk.id && d.isOpen,
  )
  const destination = (deskId: string | undefined): IntakeDeskDto | undefined =>
    destinations.find((d) => d.id === deskId)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<RerouteFormValues>({
      onSubmit: async (values, form) => {
        try {
          await reroute({
            requestId: request.summary.id,
            deskId: values.deskId,
            statusId: values.statusId,
            confirmWiderVisibility: values.confirmWiderVisibility ?? false,
          }).unwrap()
          messageApi.success('Request moved.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(apiError.detail ?? 'Failed to move the request.')
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to move the request.',
    })

  return (
    <Modal
      title="Move to Another Desk"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Move"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={600}
    >
      <Paragraph type="secondary">
        Moving a request hands the decision to the desk that should own it. It
        can go to any desk open to new requests, including one you do not work.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="reroute-intake-request-form"
        onValuesChange={(changed) => {
          // A confirmation covers what the chosen desk exposes; another desk may expose more. The arrival
          // status belongs to the chosen desk's workflow, so it starts again too.
          if ('deskId' in changed) {
            form.setFieldValue('confirmWiderVisibility', false)
            form.setFieldValue('statusId', undefined)
          }
        }}
      >
        <Item
          name="deskId"
          label="Desk"
          rules={[{ required: true, message: 'Choose a desk.' }]}
        >
          <Select
            placeholder="Select a desk"
            options={destinations.map((d) => ({ label: d.name, value: d.id }))}
          />
        </Item>
        <Item
          noStyle
          shouldUpdate={(previous, currentValues) =>
            previous.deskId !== currentValues.deskId
          }
        >
          {({ getFieldValue }) => {
            const to = destination(getFieldValue('deskId'))
            if (!to) return null

            const notice = current
              ? wideningNotice(current.visibility, to.visibility)
              : null
            const assignee = request.summary.assignee
            const losesAssignee =
              !!assignee &&
              !to.members.some((m) => m.employee.id === assignee.id)

            return (
              <>
                <ArrivalStatusField
                  key={to.id}
                  requestId={request.summary.id}
                  deskId={to.id}
                  form={form}
                />
                {losesAssignee && (
                  <Alert
                    type="info"
                    showIcon
                    title={`${assignee!.name} does not work ${to.name}, so the request will be unassigned.`}
                    style={{ marginBottom: 12 }}
                  />
                )}
                {notice && (
                  <>
                    <Alert
                      type="warning"
                      showIcon
                      title={`${to.name} is less private than ${current!.name}`}
                      description={`${notice} The requester wrote it expecting it to stay with ${current!.name}.`}
                      style={{ marginBottom: 12 }}
                    />
                    <Item
                      name="confirmWiderVisibility"
                      valuePropName="checked"
                      rules={[
                        {
                          validator: (_, checked: boolean | undefined) =>
                            checked
                              ? Promise.resolve()
                              : Promise.reject(
                                  new Error(
                                    'Confirm that the request may be seen by more people.',
                                  ),
                                ),
                        },
                      ]}
                    >
                      <Checkbox>
                        I understand this makes the request visible to more
                        people
                      </Checkbox>
                    </Item>
                  </>
                )}
              </>
            )
          }}
        </Item>
      </Form>
    </Modal>
  )
}

export default RerouteIntakeRequestForm
