'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import { StoryMapEditAccess, Visibility } from '@/src/services/wayd-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { useGetTeamOptionsQuery } from '@/src/store/features/organizations/team-api'
import { Form, Radio, Select } from 'antd'
import { FC } from 'react'

const { Item, useWatch, useFormInstance } = Form

/** The access part of a story map form. */
export interface StoryMapAccessValues {
  ownerIds: string[]
  visibility: Visibility
  editAccess: StoryMapEditAccess
  editorIds: string[]
  editorTeamIds: string[]
}

const visibilityOptions = [
  { label: 'Public', value: Visibility.Public },
  { label: 'Private', value: Visibility.Private },
]

const editAccessOptions = [
  { label: 'Anyone who can view', value: StoryMapEditAccess.AnyoneWhoCanView },
  {
    label: 'Owners and editors only',
    value: StoryMapEditAccess.OwnersAndEditors,
  },
]

export interface StoryMapAccessFieldsProps {
  /**
   * The owner who must stay on the list, such as the creator; others can be
   * added. Omit to let the list change freely.
   */
  requiredOwnerId?: string
  /**
   * The signed-in user's employee, to warn them when they take themselves off
   * the owners and so lose the ability to manage the map.
   */
  selfEmployeeId?: string
}

/**
 * Owners, visibility, who can edit, and editors for a story map form. Who can
 * edit applies only to a public map, so it is shown only then.
 */
const StoryMapAccessFields: FC<StoryMapAccessFieldsProps> = ({
  requiredOwnerId,
  selfEmployeeId,
}) => {
  const form = useFormInstance()
  const visibility = useWatch('visibility', form)
  const ownerIds: string[] | undefined = useWatch('ownerIds', form)
  const removingSelf =
    !!selfEmployeeId && !!ownerIds && !ownerIds.includes(selfEmployeeId)
  const { data: employeeOptions } = useGetEmployeeOptionsQuery(false)
  const { data: teamOptions } = useGetTeamOptionsQuery(false)

  return (
    <>
      <Item
        name="ownerIds"
        label="Owners"
        tooltip="Owners manage the map: its details, archiving, deletion and who can see and edit it."
        extra={
          removingSelf
            ? 'You are not on this list, so after saving you will no longer be able to manage this map.'
            : undefined
        }
        rules={[
          { required: true, message: 'Select at least one owner' },
          {
            validator: async (_, value: string[] | undefined) => {
              if (requiredOwnerId && !(value ?? []).includes(requiredOwnerId)) {
                throw new Error('You must be one of the owners.')
              }
            },
          },
        ]}
      >
        <EmployeeSelect
          employees={employeeOptions ?? []}
          allowMultiple
          placeholder="Select one or more owners"
        />
      </Item>
      <Item
        name="visibility"
        label="Visibility"
        rules={[{ required: true }]}
        extra={
          visibility === Visibility.Private
            ? 'Only owners and editors can see and edit this map.'
            : 'Anyone who can view Story Maps can see this map.'
        }
      >
        <Radio.Group
          options={visibilityOptions}
          optionType="button"
          buttonStyle="solid"
        />
      </Item>
      {visibility !== Visibility.Private && (
        <Item
          name="editAccess"
          label="Who Can Edit"
          rules={[{ required: true }]}
          extra="Who can change goals, steps, tasks, swim lanes and personas. Owners and editors always can."
        >
          <Radio.Group
            options={editAccessOptions}
            optionType="button"
            buttonStyle="solid"
          />
        </Item>
      )}
      <Item
        name="editorIds"
        label="Editors"
        extra="Editors can change the map's content, and see it when it is private."
      >
        <EmployeeSelect
          employees={employeeOptions ?? []}
          allowMultiple
          placeholder="Select people who can edit"
        />
      </Item>
      <Item
        name="editorTeamIds"
        label="Editor Teams"
        extra="A team's current members can edit, and so can the members of its team of teams."
      >
        <Select
          mode="multiple"
          allowClear
          placeholder="Select teams who can edit"
          options={teamOptions ?? []}
          showSearch={{ optionFilterProp: 'label' }}
        />
      </Item>
    </>
  )
}

export default StoryMapAccessFields
