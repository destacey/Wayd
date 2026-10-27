'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import {
  EmployeeNavigationDto,
  IntakeRequestNeedByDto,
  IntakeRequestNeedByRequest,
  NeedByBasis,
  NeedByBasisDto,
  NeedByBasisFamily,
  NeedByBasisFamilyDto,
} from '@/src/services/wayd-api'
import {
  formatCalendarDate,
  parseCalendarDate,
  toCalendarDate,
} from '@/src/utils'
import { DatePicker, Flex, Form, Input, Select, Tag, Typography } from 'antd'
import type { Dayjs } from 'dayjs'
import type { SelectProps } from 'antd'
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
  families: NeedByBasisFamilyDto[] | undefined
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
  families,
}) => {
  const basisDto = basisNamed(bases, basis)
  const family = families?.find((f) => f.code === basisDto?.family)
  const commitment = [
    committedByName && `made by ${committedByName}`,
    committedOn && `on ${formatCalendarDate(committedOn)}`,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <Flex vertical gap={2}>
      <Flex gap={8} align="center">
        <Text strong>{formatCalendarDate(date)}</Text>
        {family && <Tag title={family.description}>{family.name}</Tag>}
      </Flex>
      <Text>
        {basisDto?.name ?? basis}
        {reference && <Text type="secondary"> — {reference}</Text>}
      </Text>
      {commitment && <Text type="secondary">Commitment {commitment}</Text>}
    </Flex>
  )
}

/**
 * What makes the date, in the few words a list or card has room for. A fixed date reads as a fact and a
 * chosen one names who chose it, so a reviewer scanning a column can tell which dates will not move.
 */
export const NEED_BY_SHORT: Record<NeedByBasis, string> = {
  [NeedByBasis.Regulatory]: 'a regulation sets it',
  [NeedByBasis.Contractual]: 'a contract names it',
  [NeedByBasis.ExternalEvent]: 'fixed by an event',
  [NeedByBasis.Commitment]: 'we promised it',
  [NeedByBasis.RequesterPreference]: 'requester would like it',
}

/**
 * The colour a need-by is shown in: a date fixed for us is the one that cannot move, a commitment can move
 * only by talking to whoever made it, and a preference is weighed with everything else.
 */
export const needByColor = (
  needBy: IntakeRequestNeedByDto,
): string | undefined =>
  needBy.family === NeedByBasisFamily.FixedForUs
    ? 'error'
    : needBy.basis === NeedByBasis.Commitment
      ? 'warning'
      : undefined

/** A need-by in a list or on a card: "By Mar 31, 2027 · we promised it", or that no date was given. */
export const NeedByTag: FC<{ needBy: IntakeRequestNeedByDto | undefined }> = ({
  needBy,
}) =>
  needBy ? (
    <Tag color={needByColor(needBy)} title={needBy.reference || undefined}>
      By {formatCalendarDate(needBy.date)} · {NEED_BY_SHORT[needBy.basis]}
    </Tag>
  ) : (
    <Tag>No date given</Tag>
  )

/** The need-by of a request, read from its summary. */
export const RequestNeedBy: FC<{
  needBy: IntakeRequestNeedByDto
  bases: NeedByBasisDto[] | undefined
  families: NeedByBasisFamilyDto[] | undefined
}> = ({ needBy, bases, families }) => (
  <NeedBySummary
    date={needBy.date}
    basis={needBy.basis}
    reference={needBy.reference}
    committedByName={needBy.committedBy?.name}
    committedOn={needBy.committedOn}
    bases={bases}
    families={families}
  />
)

export interface NeedByFieldsProps {
  bases: NeedByBasisDto[] | undefined
  families: NeedByBasisFamilyDto[] | undefined
  employees: EmployeeNavigationDto[] | undefined
}

/**
 * The need-by question: a date, and once there is one, what makes it that date. Choosing a basis asks only
 * the follow-up that basis needs, and the preview shows the requester what reviewers will read.
 *
 * The follow-ups render from `shouldUpdate` rather than `Form.useWatch`: a watch on a field whose item
 * mounts later reads undefined, so an edit form opening on a saved basis would hide its own reference.
 */
export const NeedByFields: FC<NeedByFieldsProps> = ({
  bases,
  families,
  employees,
}) => {
  const basisOption = (b: NeedByBasisDto) => ({
    value: b.code,
    label: b.name,
    title: b.description,
  })
  // Grouped by family, so the requester answers "is this fixed for us or did we choose it?" in passing.
  const basisOptions: SelectProps['options'] = families?.length
    ? [...families]
        .sort((a, b) => a.order - b.order)
        .map((f) => ({
          label: f.name,
          title: f.description,
          options: (bases ?? [])
            .filter((b) => b.family === f.code)
            .map(basisOption),
        }))
    : (bases ?? []).map(basisOption)

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
      <Item
        noStyle
        shouldUpdate={(previous, current) => previous.needBy !== current.needBy}
      >
        {({ getFieldValue }) => {
          const needBy = (getFieldValue('needBy') ?? {}) as NeedByFormValues
          const basisDto = basisNamed(bases, needBy.basis)
          if (!needBy.date) return null

          return (
            <>
              <Item
                name={['needBy', 'basis']}
                label="What makes it that date?"
                rules={[
                  { required: true, message: 'Choose what sets the date.' },
                ]}
              >
                <Select
                  placeholder="Choose a basis"
                  options={basisOptions}
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
              {basisDto && (
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
              {basisDto?.recordsCommitment && (
                <>
                  <Item
                    name={['needBy', 'committedById']}
                    label="Who made the commitment?"
                    rules={[
                      {
                        required: true,
                        message: 'Name who made the commitment.',
                      },
                    ]}
                  >
                    <EmployeeSelect employees={employeeOptions} />
                  </Item>
                  <Item
                    name={['needBy', 'committedOn']}
                    label="When was it made?"
                    rules={[
                      { required: true, message: 'Give the date it was made.' },
                    ]}
                  >
                    <DatePicker />
                  </Item>
                </>
              )}
              {basisDto && (
                <Item label="Reviewers will see">
                  <NeedBySummary
                    date={toCalendarDate(needBy.date)}
                    basis={basisDto.code}
                    reference={needBy.reference?.trim() ?? ''}
                    committedByName={
                      basisDto.recordsCommitment
                        ? employees?.find((e) => e.id === needBy.committedById)
                            ?.name
                        : undefined
                    }
                    committedOn={
                      basisDto.recordsCommitment && needBy.committedOn
                        ? toCalendarDate(needBy.committedOn)
                        : undefined
                    }
                    bases={bases}
                    families={families}
                  />
                </Item>
              )}
            </>
          )
        }}
      </Item>
    </>
  )
}
