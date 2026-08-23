import { Form, Input } from 'antd'

const { Item } = Form

export interface SecretFormItemProps {
  label: string
  name: string
  maxLength: number
  mode: 'create' | 'edit'
  /** Extra guidance appended after the edit-mode "leave blank" note. */
  extra?: string
}

/**
 * A connector secret input. On edit it starts blank and is optional — the API never returns the
 * stored credential, and a blank submission means "keep it". Marking it required in edit mode
 * would force admins to re-enter a secret they may not have just to change an unrelated field.
 */
export const SecretFormItem: React.FC<SecretFormItemProps> = ({
  label,
  name,
  maxLength,
  mode,
  extra,
}) => {
  const isEdit = mode === 'edit'
  const keepNote = 'Leave blank to keep the current value.'

  return (
    <Item
      label={label}
      name={name}
      rules={[{ required: !isEdit }]}
      extra={isEdit ? [keepNote, extra].filter(Boolean).join(' ') : extra}
    >
      <Input.Password
        maxLength={maxLength}
        autoComplete="new-password"
        placeholder={isEdit ? 'Unchanged' : undefined}
      />
    </Item>
  )
}
