'use client'

import { EstimationScaleValueRequest } from '@/src/services/wayd-api'
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Form, Input, Segmented, Space, Typography } from 'antd'
import type { NamePath } from 'antd/es/form/interface'
import { ReactNode } from 'react'

const { Item, List, ErrorList } = Form
const { Text } = Typography

/** The fewest estimate values a scale may have, as the API enforces. */
export const MINIMUM_ESTIMATE_VALUES = 2

const kindOptions = [
  { label: 'Estimate', value: 'estimate' },
  { label: 'Card', value: 'card' },
]

/** A new row: most values added to a scale are sizes. */
export const newEstimationScaleValue = (): EstimationScaleValueRequest => ({
  value: '',
  isEstimate: true,
})

/**
 * Checks a scale's values as the API will: enough estimates, and no value
 * twice once trimmed. Exported so the rule can be tested without a form.
 */
export const validateEstimationScaleValues = (
  values: EstimationScaleValueRequest[] | undefined,
): string | undefined => {
  const list = values ?? []
  if (list.filter((v) => v?.isEstimate).length < MINIMUM_ESTIMATE_VALUES) {
    return `At least ${MINIMUM_ESTIMATE_VALUES} values must be estimates`
  }
  const seen = new Set<string>()
  for (const entry of list) {
    const value = entry?.value?.trim()
    if (!value) continue
    if (seen.has(value)) return `"${value}" appears more than once`
    seen.add(value)
  }
  return undefined
}

/**
 * A `toFormErrors` `fieldFor` that puts the API's errors for one value
 * (`values[2].value`) on that row's input; the list's own errors (`values`)
 * already match the list by name.
 */
export const estimationScaleValueErrorFieldFor = (
  key: string,
): NamePath | undefined => {
  const match = /^values\[(\d+)\]\.(value|isEstimate)$/.exec(key)
  return match ? ['values', Number(match[1]), 'value'] : undefined
}

export interface EstimationScaleValuesFieldProps {
  /** Shown under the list, before any error. */
  extra?: ReactNode
}

/**
 * The ordered values of an estimation scale, each marked as an estimate (a
 * size) or a card (such as `?`, votable but never an estimate). Bound to the
 * form's `values` field as `EstimationScaleValueRequest[]`.
 */
const EstimationScaleValuesField = ({
  extra,
}: EstimationScaleValuesFieldProps) => {
  return (
    <Item label="Scale Values" extra={extra}>
      <List
        name="values"
        rules={[
          {
            validator: async (_, values) => {
              const error = validateEstimationScaleValues(values)
              if (error) return Promise.reject(new Error(error))
            },
          },
        ]}
      >
        {(fields, { add, remove }, { errors }) => (
          <>
            {fields.map(({ key, ...field }) => (
              <Space
                key={key}
                style={{ display: 'flex', marginBottom: 4 }}
                align="baseline"
              >
                {/* `key` is destructured off above: Form.List's field carries
                  one, and spreading it into an element is a React warning
                  rather than a working key. */}
                <Item
                  name={[field.name, 'value']}
                  rules={[
                    {
                      required: true,
                      whitespace: true,
                      message: 'Value is required',
                    },
                    { max: 32 },
                  ]}
                  style={{ marginBottom: 0 }}
                >
                  <Input
                    placeholder={`Value ${field.name + 1}`}
                    maxLength={32}
                    aria-label={`Value ${field.name + 1}`}
                  />
                </Item>
                <Item
                  name={[field.name, 'isEstimate']}
                  style={{ marginBottom: 0 }}
                  getValueProps={(isEstimate: boolean) => ({
                    value: isEstimate ? 'estimate' : 'card',
                  })}
                  normalize={(kind: string) => kind === 'estimate'}
                >
                  <Segmented
                    options={kindOptions}
                    aria-label={`Value ${field.name + 1} kind`}
                  />
                </Item>
                {fields.length > MINIMUM_ESTIMATE_VALUES && (
                  <MinusCircleOutlined
                    aria-label={`Remove value ${field.name + 1}`}
                    onClick={() => remove(field.name)}
                  />
                )}
              </Space>
            ))}
            <Item>
              <Button
                type="dashed"
                onClick={() => add(newEstimationScaleValue())}
                block
                icon={<PlusOutlined />}
              >
                Add Value
              </Button>
              {/* The list's rule spans every row, but antd re-runs and shows
                  it only when a row is added or removed or the form is
                  submitted. Once a row has changed, the same rule is checked
                  on every change, so the reason the form cannot be saved shows
                  as soon as it applies. */}
              <Item noStyle shouldUpdate>
                {(form) => {
                  const liveError = form.isFieldsTouched()
                    ? validateEstimationScaleValues(
                        form.getFieldValue('values'),
                      )
                    : undefined
                  // Plain text rather than ErrorList, whose animated list drops
                  // a message it did not receive through a field's validation.
                  return errors.length > 0 || !liveError ? (
                    <ErrorList errors={errors} />
                  ) : (
                    <Text type="danger" role="alert">
                      {liveError}
                    </Text>
                  )
                }}
              </Item>
            </Item>
          </>
        )}
      </List>
    </Item>
  )
}

export default EstimationScaleValuesField
