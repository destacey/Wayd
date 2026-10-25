'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import {
  EmployeeNavigationDto,
  IntakeRequestNeedByDto,
  IntakeRequestNeedByRequest,
  NeedByBasis,
  NeedByBasisDto,
} from '@/src/services/wayd-api'
import {
  formatCalendarDate,
  parseCalendarDate,
  toCalendarDate,
} from '@/src/utils'
import { DatePicker, Flex, Form, Input, Select, Typography } from 'antd'
import type { Dayjs } from 'dayjs'
import type { NamePath } from 'antd/es/form/interface'
import { FC } from 'react'

const { Item } = Form
const { Text } = Typography

/** The need-by fields as a form holds them, nested under `needBy`. */
export interface NeedByFormValues {
  date?: Dayjs | null
  basis?: NeedByBasis
  reference?: string
  committedById?: string
  committedOn?: Dayjs | null
}

/** A request's need-by as the form's starting values. */
export const toNeedByFormValues = (
  needBy: IntakeRequestNeedByDto | undefined,
): NeedByFormValues =>
  needBy
    ? {
        date: parseCalendarDate(needBy.date),
        basis: needBy.basis,
        reference: needBy.reference,
        committedById: needBy.committedBy?.id,
        committedOn: needBy.committedOn
          ? parseCalendarDate(needBy.committedOn)
          : undefined,
      }
    : {}

/**
 * The need-by a request is sent with, or undefined when it has none. The date, its basis and its reference
 * travel together, and the commitment fields only with a basis that records one.
 */
export const toNeedByRequest = (
  values: NeedByFormValues | undefined,
  bases: NeedByBasisDto[] | undefined,
): IntakeRequestNeedByRequest | undefined => {
  if (!values?.date || !values.basis) return undefined
  const recordsCommitment = bases?.find(
    (b) => b.code === values.basis,
  )?.recordsCommitment
  return {
    date: toCalendarDate(values.date),
    basis: values.basis,
    reference: values.reference?.trim() ?? '',
    committedById: recordsCommitment ? values.committedById : undefined,
    committedOn:
      recordsCommitment && values.committedOn
        ? toCalendarDate(values.committedOn)
        : undefined,
  }
}

/** Puts the API's `NeedBy.*` validation errors on the nested need-by fields. */
export const needByFieldFor = (key: string): NamePath | undefined =>
  key.startsWith('needBy.')
    ? ['needBy', key.slice('needBy.'.length)]
    : undefined

const basisNamed = (
  bases: NeedByBasisDto[] | undefined,
  code: NeedByBasis | undefined,
) => bases?.find((b) => b.code === code)

export interface NeedBySummaryProps {
  date: string
  basis: NeedByBasis
  reference: string
  committedByName?: string
  committedOn?: string
  bases: NeedByBasisDto[] | undefined
}

/**
 * A need-by as reviewers read it: the date, what makes it that date, and what the date rests on. The basis
 * is named from the lookup, never from its code.
 */
export const NeedBySummary: FC<NeedBySummaryProps> = ({
  date,
  basis,
  reference,
  committedByName,
  committedOn,
  bases,
}) => {
  const basisDto = basisNamed(bases, basis)
  const commitment = [
    committedByName && `made by ${committedByName}`,
    committedOn && `on ${formatCalendarDate(committedOn)}`,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <Flex vertical gap={2}>
      <Text strong>{formatCalendarDate(date)}</Text>
      <Text>
        {basisDto?.name ?? basis}
        {reference && <Text type="secondary"> — {reference}</Text>}
      </Text>
      {commitment && <Text type="secondary">Commitment {commitment}</Text>}
    </Flex>
  )
}

/** The need-by of a request, read from its summary. */
export const RequestNeedBy: FC<{
  needBy: IntakeRequestNeedByDto
  bases: NeedByBasisDto[] | undefined
}> = ({ needBy, bases }) => (
  <NeedBySummary
    date={needBy.date}
    basis={needBy.basis}
    reference={needBy.reference}
    committedByName={needBy.committedBy?.name}
    committedOn={needBy.committedOn}
    bases={bases}
  />
)

export interface NeedByFieldsProps {
  bases: NeedByBasisDto[] | undefined
  employees: EmployeeNavigationDto[] | undefined
}

/**
 * The need-by question: a date, and once there is one, what makes it that date. Choosing a basis asks only
 * the follow-up that basis needs, and the preview shows the requester what reviewers will read.
 */
export const NeedByFields: FC<NeedByFieldsProps> = ({ bases, employees }) => {
  const form = Form.useFormInstance()
  const date = Form.useWatch(['needBy', 'date'], form) as Dayjs | null
  const basis = Form.useWatch(['needBy', 'basis'], form) as
    NeedByBasis | undefined
  const reference = Form.useWatch(['needBy', 'reference'], form) as
    string | undefined
  const committedById = Form.useWatch(['needBy', 'committedById'], form) as
    string | undefined
  const committedOn = Form.useWatch(
    ['needBy', 'committedOn'],
    form,
  ) as Dayjs | null

  const basisDto = basisNamed(bases, basis)
  const hasDate = !!date

  const employeeOptions = (employees ?? []).map((e) => ({
    label: e.name,
    value: e.id,
  }))

  return (
    <>
      <Item
        name={['needBy', 'date']}
        label="Need by"
        extra="Optional. Leave it empty if nothing sets a date."
      >
        <DatePicker allowClear />
      </Item>
      {hasDate && (
        <Item
          name={['needBy', 'basis']}
          label="What makes it that date?"
          rules={[{ required: true, message: 'Choose what sets the date.' }]}
        >
          <Select
            placeholder="Choose a basis"
            options={(bases ?? []).map((b) => ({
              value: b.code,
              label: b.name,
              title: b.description,
            }))}
            optionRender={(option) => (
              <Flex vertical>
                <Text>{option.data.label}</Text>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {option.data.title}
                </Text>
              </Flex>
            )}
          />
        </Item>
      )}
      {hasDate && basisDto && (
        <Item
          name={['needBy', 'reference']}
          label="What does the date rest on?"
          extra={basisDto.description}
          rules={[
            {
              required: true,
              whitespace: true,
              message: 'Say what the date rests on.',
            },
            { max: 1024 },
          ]}
        >
          <Input.TextArea
            autoSize={{ minRows: 1, maxRows: 4 }}
            maxLength={1024}
          />
        </Item>
      )}
      {hasDate && basisDto?.recordsCommitment && (
        <>
          <Item
            name={['needBy', 'committedById']}
            label="Who made the commitment?"
            rules={[
              { required: true, message: 'Name who made the commitment.' },
            ]}
          >
            <EmployeeSelect employees={employeeOptions} />
          </Item>
          <Item
            name={['needBy', 'committedOn']}
            label="When was it made?"
            rules={[{ required: true, message: 'Give the date it was made.' }]}
          >
            <DatePicker />
          </Item>
        </>
      )}
      {hasDate && basis && (
        <Item label="Reviewers will see">
          <NeedBySummary
            date={toCalendarDate(date)}
            basis={basis}
            reference={reference?.trim() ?? ''}
            committedByName={
              basisDto?.recordsCommitment
                ? employees?.find((e) => e.id === committedById)?.name
                : undefined
            }
            committedOn={
              basisDto?.recordsCommitment && committedOn
                ? toCalendarDate(committedOn)
                : undefined
            }
            bases={bases}
          />
        </Item>
      )}
    </>
  )
}
