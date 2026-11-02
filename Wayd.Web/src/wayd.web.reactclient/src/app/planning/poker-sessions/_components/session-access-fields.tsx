'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import { Visibility } from '@/src/services/wayd-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { useGetTeamOptionsQuery } from '@/src/store/features/organizations/team-api'
import { Form, Radio, Select } from 'antd'
import { FC } from 'react'

const { Item, useWatch, useFormInstance } = Form

/** The access part of a poker session form. */
export interface SessionAccessValues {
  visibility: Visibility
  facilitatorIds: string[]
  invitedEmployeeIds: string[]
  invitedTeamIds: string[]
}

const visibilityOptions = [
  { label: 'Public', value: Visibility.Public },
  { label: 'Private', value: Visibility.Private },
]

export interface SessionAccessFieldsProps {
  /**
   * The facilitator who must stay on the list, such as the creator; others
   * can be added. Omit to let the list change freely.
   */
  requiredFacilitatorId?: string
}

/**
 * Visibility, facilitators and invites for a poker session form. Invites apply
 * only while the session is private, so they are shown only then.
 */
const SessionAccessFields: FC<SessionAccessFieldsProps> = ({
  requiredFacilitatorId,
}) => {
  const form = useFormInstance()
  const visibility = useWatch('visibility', form)
  const { data: employeeOptions } = useGetEmployeeOptionsQuery(false)
  const { data: teamOptions } = useGetTeamOptionsQuery(false)

  return (
    <>
      <Item
        name="facilitatorIds"
        label="Facilitators"
        tooltip="Facilitators run the session: rounds, reveals, consensus, access and completion."
        rules={[
          { required: true, message: 'Select at least one facilitator' },
          {
            validator: async (_, value: string[] | undefined) => {
              if (
                requiredFacilitatorId &&
                !(value ?? []).includes(requiredFacilitatorId)
              ) {
                throw new Error('You must be one of the facilitators.')
              }
            },
          },
        ]}
      >
        <EmployeeSelect
          employees={employeeOptions ?? []}
          allowMultiple
          placeholder="Select one or more facilitators"
        />
      </Item>
      <Item
        name="visibility"
        label="Visibility"
        rules={[{ required: true }]}
        extra={
          visibility === Visibility.Private
            ? 'Only facilitators and the people and teams invited can see and join.'
            : 'Anyone who can view Planning Poker can see and join.'
        }
      >
        <Radio.Group
          options={visibilityOptions}
          optionType="button"
          buttonStyle="solid"
        />
      </Item>
      {visibility === Visibility.Private && (
        <>
          <Item
            name="invitedEmployeeIds"
            label="Invited People"
            extra="An invite lets someone in; they choose to join as a Participant or an Observer."
          >
            <EmployeeSelect
              employees={employeeOptions ?? []}
              allowMultiple
              placeholder="Select people to invite"
            />
          </Item>
          <Item
            name="invitedTeamIds"
            label="Invited Teams"
            extra="A team's current members are invited, and so are the members of its team of teams."
          >
            <Select
              mode="multiple"
              allowClear
              placeholder="Select teams to invite"
              options={teamOptions ?? []}
              showSearch={{ optionFilterProp: 'label' }}
            />
          </Item>
        </>
      )}
    </>
  )
}

export default SessionAccessFields
