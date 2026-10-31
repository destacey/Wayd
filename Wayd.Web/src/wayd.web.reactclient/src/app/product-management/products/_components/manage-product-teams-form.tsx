'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProductDto } from '@/src/services/wayd-api'
import { useGetTeamOptionsQuery } from '@/src/store/features/organizations/team-api'
import { useSetProductTeamsMutation } from '@/src/store/features/product-management/products-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Modal, Select, Typography } from 'antd'
import { useState } from 'react'

const { Item } = Form
const { Text } = Typography

export interface ManageProductTeamsFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ManageProductTeamsFormValues {
  teamIds: string[]
}

/**
 * Sets the teams linked to a product as a whole: selected teams are linked, removed ones unlinked. Only the
 * product's own links; the teams on products beneath it are managed on those products.
 */
const ManageProductTeamsForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: ManageProductTeamsFormProps) => {
  const messageApi = useMessage()

  // Active teams only, since only they can be newly linked.
  const { data: teamOptions, isLoading: teamsLoading } =
    useGetTeamOptionsQuery(false)
  const [setProductTeams] = useSetProductTeamsMutation()

  // A linked team that has since been deactivated is not in the active list, but stays selectable so it can be
  // kept or removed, and shows its name rather than its id.
  const offered = new Set(teamOptions?.map((option) => option.value))
  const options = [
    ...(teamOptions ?? []),
    ...product.teams
      .filter((team) => !offered.has(team.id))
      .map((team) => ({ value: team.id, label: `${team.name} (inactive)` })),
  ]

  const currentTeamIds = product.teams.map((team) => team.id)

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<ManageProductTeamsFormValues>({
      onSubmit: async (values: ManageProductTeamsFormValues, form) => {
        try {
          const response = await setProductTeams({
            productId: product.id,
            teamIds: values.teamIds ?? [],
          })
          if (response.error) throw response.error

          messageApi.success('Teams updated.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the teams. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the teams. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  const [selected, setSelected] = useState<string[]>(currentTeamIds)
  const chosen = new Set(selected)
  const current = new Set(currentTeamIds)
  const toLink = selected.filter((id) => !current.has(id)).length
  const toUnlink = currentTeamIds.filter((id) => !chosen.has(id)).length
  const hasChanges = toLink > 0 || toUnlink > 0

  return (
    <Modal
      title="Manage Teams"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !hasChanges }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="manage-product-teams-form"
        initialValues={{ teamIds: currentTeamIds }}
        onValuesChange={(_, values: ManageProductTeamsFormValues) =>
          setSelected(values.teamIds ?? [])
        }
      >
        <Item
          name="teamIds"
          label="Teams"
          extra={`The teams that work on ${product.name}. Link each team to the most specific product it works on; ${product.name}'s parents show its teams too.`}
        >
          <Select
            mode="multiple"
            allowClear
            showSearch={{ optionFilterProp: 'label' }}
            placeholder="Select teams or teams of teams"
            loading={teamsLoading}
            options={options}
          />
        </Item>
      </Form>
      <Text type="secondary" aria-live="polite">
        {hasChanges ? `${toLink} to link, ${toUnlink} to unlink` : 'No changes'}
      </Text>
    </Modal>
  )
}

export default ManageProductTeamsForm
