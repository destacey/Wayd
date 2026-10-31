'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProductDto } from '@/src/services/wayd-api'
import { useGetTeamOptionsQuery } from '@/src/store/features/organizations/team-api'
import { useLinkProductTeamMutation } from '@/src/store/features/product-management/products-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Modal, Select } from 'antd'

const { Item } = Form

export interface LinkProductTeamFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface LinkProductTeamFormValues {
  teamId: string
}

/**
 * Links a team or team of teams to a product it works on. Any number of teams may be linked.
 */
const LinkProductTeamForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: LinkProductTeamFormProps) => {
  const messageApi = useMessage()

  // Active teams only: an inactive team cannot be linked.
  const { data: teamOptions, isLoading: teamsLoading } =
    useGetTeamOptionsQuery(false)
  const [linkProductTeam] = useLinkProductTeamMutation()

  // Teams already linked here are left out: linking one again would change nothing.
  const linkedTeamIds = new Set(product.teams.map((t) => t.id))
  const availableTeams = teamOptions?.filter(
    (option) => !linkedTeamIds.has(option.value),
  )

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<LinkProductTeamFormValues>({
      onSubmit: async (values: LinkProductTeamFormValues, form) => {
        try {
          const response = await linkProductTeam({
            productId: product.id,
            teamId: values.teamId,
          })
          if (response.error) throw response.error

          messageApi.success('Team linked.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while linking the team. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while linking the team. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  return (
    <Modal
      title="Link Team"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Link"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="link-product-team-form"
      >
        <Item
          name="teamId"
          label="Team"
          rules={[{ required: true, message: 'Select a team' }]}
          extra={`Link the most specific product the team works on. ${product.name}'s parents show it too.`}
        >
          <Select
            showSearch={{ optionFilterProp: 'label' }}
            placeholder="Select a team or team of teams"
            loading={teamsLoading}
            options={availableTeams}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default LinkProductTeamForm
