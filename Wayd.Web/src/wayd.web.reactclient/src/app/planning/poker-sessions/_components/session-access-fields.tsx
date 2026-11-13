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

const visibilityOptions = (alwaysPrivate: boolean) => [
  { label: 'Public', value: Visibility.Public, disabled: alwaysPrivate },
  { label: 'Private', value: Visibility.Private },
]

export interface SessionAccessFieldsProps {
  /**
   * The facilitator who must stay on the list, such as the creator; others
   * can be added. Omit to let the list change freely.
   */
  requiredFacilitatorId?: string
  /**
   * The signed-in user's employee, to warn them when they take themselves off
   * the facilitators and so lose the ability to run the session.
   */
  selfEmployeeId?: string
  /**
   * The session belongs to another area of Wayd, such as an intake desk,
   * which keeps it private.
   */
  alwaysPrivate?: boolean
}

/**
 * Visibility, facilitators and invites for a poker session form. Invites apply
 * only while the session is private, so they are shown only then.
 */
const SessionAccessFields: FC<SessionAccessFieldsProps> = ({
  requiredFacilitatorId,
  selfEmployeeId,
  alwaysPrivate = false,
}) => {
  const form = useFormInstance()
  const visibility = useWatch('visibility', form)
  const facilitatorIds: string[] | undefined = useWatch('facilitatorIds', form)
  const removingSelf =
    !!selfEmployeeId &&
    !!facilitatorIds &&
    !facilitatorIds.includes(selfEmployeeId)
  const { data: employeeOptions } = useGetEmployeeOptionsQuery(false)
  const { data: teamOptions } = useGetTeamOptionsQuery(false)

  return (
    <>
      <Item
        name="facilitatorIds"
        label="Facilitators"
        tooltip="Facilitators run the session: rounds, reveals, consensus, access and completion."
        extra={
          removingSelf
            ? 'You are not on this list, so after saving you will no longer be able to run this session.'
            : undefined
        }
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
          alwaysPrivate
            ? 'This session belongs to another area of Wayd, so it is always private. Those who manage that area can also see and run it.'
            : visibility === Visibility.Private
              ? 'Only facilitators and the people and teams invited can see and join.'
              : 'Anyone who can view Planning Poker can see and join.'
        }
      >
        <Radio.Group
          options={visibilityOptions(alwaysPrivate)}
          optionType="button"
          buttonStyle="solid"
        />
      </Item>
      {visibility === Visibility.Private && (
        <>
          <Item
            name="invitedEmployeeIds"
            label="Invited People"
            extra="An invite lets someone in; they choose whether to vote."
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
