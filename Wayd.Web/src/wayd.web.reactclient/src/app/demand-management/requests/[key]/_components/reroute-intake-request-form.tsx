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
  useRerouteIntakeRequestMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Alert, Checkbox, Form, Modal, Select, Typography } from 'antd'
import { FC } from 'react'

const { Item } = Form
const { Paragraph } = Typography

const privacy: Record<IntakeDeskVisibility, number> = {
  [IntakeDeskVisibility.Open]: 1,
  [IntakeDeskVisibility.SummaryOnly]: 2,
  [IntakeDeskVisibility.Private]: 3,
}

/**
 * What a move from one desk to another makes visible, and to whom, or null when it makes nothing more
 * visible. Names only what a request holds today; the server refuses the move unless it is confirmed.
 */
export const wideningNotice = (
  from: IntakeDeskVisibility,
  to: IntakeDeskVisibility,
): string | null => {
  if (privacy[to] >= privacy[from]) return null

  const summary = 'its title, requester, channel and need-by date'
  const substance = 'its description and expected benefits'
  if (from === IntakeDeskVisibility.Private) {
    return to === IntakeDeskVisibility.Open
      ? `Everyone who can view requests will see ${summary}, and ${substance}.`
      : `Everyone who can view requests will see ${summary}.`
  }
  return `Everyone who can view requests will see ${substance}.`
}

export interface RerouteIntakeRequestFormProps {
  request: IntakeRequestDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface RerouteFormValues {
  deskId: string
  confirmWiderVisibility?: boolean
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
