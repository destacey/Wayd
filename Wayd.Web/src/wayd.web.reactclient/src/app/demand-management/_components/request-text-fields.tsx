'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { IntakeChannelDto } from '@/src/services/wayd-api'
import { Form, Input, Select } from 'antd'
import { FC } from 'react'

const { Item } = Form

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
  channels: IntakeChannelDto[] | undefined
}

/** The fields every desk asks for, shared by submit and edit so the two never drift. */
const RequestTextFields: FC<RequestTextFieldsProps> = ({ channels }) => (
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
      label="Description"
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
    <Item
      name="expectedBenefits"
      label="Expected benefits"
      extra="Optional. What gets better, and for whom, if this is delivered."
    >
      <MarkdownEditor maxLength={TEXT_MAX_LENGTH} />
    </Item>
    <Item
      name="channelId"
      label="Where did this come from?"
      rules={[{ required: true, message: 'Choose a channel.' }]}
    >
      <Select
        placeholder="Choose a channel"
        options={(channels ?? []).map((c) => ({
          value: c.id,
          label: c.isActive ? c.name : `${c.name} (Archived)`,
          disabled: !c.isActive,
        }))}
      />
    </Item>
  </>
)

export default RequestTextFields
