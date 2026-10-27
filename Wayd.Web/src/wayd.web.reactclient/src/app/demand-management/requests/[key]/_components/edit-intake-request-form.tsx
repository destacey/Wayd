'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  IntakeChannelDto,
  IntakeRequestDetailsDto,
  IntakeRequestSubstanceDto,
} from '@/src/services/wayd-api'
import {
  useGetIntakeChannelsQuery,
  useGetIntakeEmployeeOptionsQuery,
  useGetNeedByBasesQuery,
  useGetNeedByBasisFamiliesQuery,
  useUpdateIntakeRequestMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Modal } from 'antd'
import { FC, useEffect } from 'react'
import { NeedByFields } from '../../../_components/need-by-fields'
import {
  NeedByFormValues,
  needByFieldFor,
  toNeedByFormValues,
  toNeedByRequest,
} from '../../../_components/need-by'
import RequestTextFields from '../../../_components/request-text-fields'

export interface EditIntakeRequestFormProps {
  request: IntakeRequestDetailsDto
  /** Editing needs the substance, which the server's `canEdit` already requires. */
  substance: IntakeRequestSubstanceDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditIntakeRequestFormValues {
  title: string
  description: string
  expectedBenefits?: string
  channelId?: string
  needBy?: NeedByFormValues
}

/**
 * Edits the request's own fields. Offered only where the server's `canEdit` says so; the desk is not here,
 * because moving a request between desks is a reviewer's re-route.
 */
const EditIntakeRequestForm: FC<EditIntakeRequestFormProps> = ({
  request,
  substance,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [updateRequest] = useUpdateIntakeRequestMutation()
  const { data: bases } = useGetNeedByBasesQuery()
  const { data: families } = useGetNeedByBasisFamiliesQuery()
  const { data: activeEmployees } = useGetIntakeEmployeeOptionsQuery()
  // The options are active employees only. A commitment stays a fact about whoever made it after they
  // leave, so keep them in the list rather than showing their id.
  const committedBy = request.summary.needBy?.committedBy
  const employees =
    committedBy && !activeEmployees?.some((e) => e.id === committedBy.id)
      ? [...(activeEmployees ?? []), committedBy]
      : activeEmployees
  // The channels the desk offers, plus the one the request already has: it keeps a channel after the
  // channel is archived or the request moves to a desk that does not offer it.
  const currentChannel = request.summary.channel
  const { data: deskChannels } = useGetIntakeChannelsQuery({
    deskId: request.summary.desk.id,
  })
  const { data: allChannels } = useGetIntakeChannelsQuery(
    { includeArchived: true },
    { skip: !currentChannel },
  )
  // The summary already names the channel, so it is offered before the full list loads, or if that fails:
  // without it the field would hide on a desk offering none, and saving would clear the channel. The full
  // list only adds whether it is archived.
  const current: IntakeChannelDto | undefined = currentChannel
    ? (allChannels?.find((c) => c.id === currentChannel.id) ?? {
        id: currentChannel.id,
        name: currentChannel.name,
        order: 0,
        isActive: true,
      })
    : undefined
  const channels =
    deskChannels && current && !deskChannels.some((c) => c.id === current.id)
      ? [...deskChannels, current]
      : deskChannels
  // A recorded channel can be changed but not removed, unless the desk offers none.
  const channelRequired = (deskChannels?.length ?? 0) > 0

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditIntakeRequestFormValues>({
      onSubmit: async (values, form) => {
        try {
          await updateRequest({
            id: request.summary.id,
            title: values.title.trim(),
            description: values.description,
            expectedBenefits: values.expectedBenefits || undefined,
            channelId: values.channelId ?? undefined,
            needBy: toNeedByRequest(values.needBy, bases),
          }).unwrap()
          messageApi.success('Request updated.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(
              toFormErrors(apiError.errors, { fieldFor: needByFieldFor }),
            )
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(apiError.detail ?? 'Failed to update the request.')
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to update the request.',
    })

  useEffect(() => {
    form.setFieldsValue({
      title: request.summary.title,
      description: substance.description,
      expectedBenefits: substance.expectedBenefits,
      channelId: request.summary.channel?.id,
      needBy: toNeedByFormValues(request.summary.needBy),
    })
  }, [form, request, substance])

  return (
    <Modal
      title="Edit Request"
      open={isOpen}
      onOk={handleOk}
      // The need-by follow-ups mount only once the bases are known; saving before then would drop a saved
      // reference and commitment from the update.
      okButtonProps={{ disabled: !isValid || !bases }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={720}
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-intake-request-form"
      >
        <RequestTextFields
          channels={channels}
          channelRequired={channelRequired}
        />
        <NeedByFields bases={bases} families={families} employees={employees} />
      </Form>
    </Modal>
  )
}

export default EditIntakeRequestForm
