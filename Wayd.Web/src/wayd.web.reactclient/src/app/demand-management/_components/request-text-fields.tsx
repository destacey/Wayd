'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { IntakeChannelDto } from '@/src/services/wayd-api'
import { Flex, Form, Input, Select, Typography } from 'antd'
import { FC } from 'react'

const { Item } = Form
const { Text } = Typography

/**
 * Said where the title is typed. Titles are written by requesters and nothing sanitises them, so the
 * summary tier is where a careless title leaks; saying so is the honest version.
 */
export const TITLE_VISIBILITY_NOTE =
  'Everyone in the company who can view requests sees the title, even when the detail is kept to the desk. Write a title you would be comfortable with anyone reading.'

/**
 * The editor needs a limit and the API sets none on the description or expected benefits; this is far
 * beyond any ask a reviewer would read, so it never refuses a real one.
 */
const TEXT_MAX_LENGTH = 20000

export interface RequestTextFieldsProps {
  /**
   * The channels to choose from: those the desk offers, plus on edit the one the request already has. An
   * empty list asks nothing, because the desk offers no channel.
   */
  channels: IntakeChannelDto[] | undefined
  /**
   * Whether a channel must be chosen: true when the desk offers any. When false the field can be cleared,
   * which only a request on a desk offering none may do.
   */
  channelRequired?: boolean
}

/**
 * What the request asks for: its title, its description and where it came from. The submit form words the
 * description as a question to the requester; the edit form names the field.
 */
export const RequestAskFields: FC<
  RequestTextFieldsProps & { descriptionLabel?: string }
> = ({
  channels,
  channelRequired = true,
  descriptionLabel = 'Description',
}) => (
  <>
    <Item
      name="title"
      label="Title"
      extra={TITLE_VISIBILITY_NOTE}
      rules={[
        { required: true, whitespace: true, message: 'Title is required.' },
        { max: 256 },
      ]}
    >
      <Input.TextArea
        autoSize={{ minRows: 1, maxRows: 2 }}
        showCount
        maxLength={256}
      />
    </Item>
    <Item
      name="description"
      label={descriptionLabel}
      rules={[
        {
          required: true,
          whitespace: true,
          message: 'Describe what you are asking for.',
        },
      ]}
    >
      <MarkdownEditor maxLength={TEXT_MAX_LENGTH} />
    </Item>
    {channels?.length !== 0 && (
      <Item
        name="channelId"
        label="Where did this come from?"
        rules={[{ required: channelRequired, message: 'Choose a channel.' }]}
      >
        <Select
          placeholder="Choose a channel"
          allowClear={!channelRequired}
          loading={channels === undefined}
          options={(channels ?? []).map((c) => ({
            value: c.id,
            label: c.isActive ? c.name : `${c.name} (Archived)`,
            description: c.description,
          }))}
          // The description says what belongs in the channel, which is what a requester chooses by; the
          // chosen value shows the name alone.
          optionRender={(option) => (
            <Flex vertical>
              <Text>{option.label}</Text>
              {option.data.description && (
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {option.data.description}
                </Text>
              )}
            </Flex>
          )}
        />
      </Item>
    )}
  </>
)

/** What the requester expects to get better. Optional, and worded as a question on the submit form. */
export const ExpectedBenefitsField: FC<{ label?: string }> = ({
  label = 'Expected benefits',
}) => (
  <Item
    name="expectedBenefits"
    label={label}
    extra="Optional. What gets better, and for whom, if this is delivered."
  >
    <MarkdownEditor maxLength={TEXT_MAX_LENGTH} />
  </Item>
)

/** The fields every desk asks for, shared by submit and edit so the two never drift. */
const RequestTextFields: FC<RequestTextFieldsProps> = ({
  channels,
  channelRequired,
}) => (
  <>
    <RequestAskFields channels={channels} channelRequired={channelRequired} />
    <ExpectedBenefitsField />
  </>
)

export default RequestTextFields
