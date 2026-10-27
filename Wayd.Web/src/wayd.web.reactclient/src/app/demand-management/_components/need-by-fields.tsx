'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import {
  EmployeeNavigationDto,
  NeedByBasis,
  NeedByBasisDto,
  NeedByBasisFamily,
  NeedByBasisFamilyDto,
} from '@/src/services/wayd-api'
import { toCalendarDate } from '@/src/utils'
import { Button, DatePicker, Flex, Form, Input, Radio, Typography } from 'antd'
import { FC } from 'react'
import { NeedByFormValues, NeedByTag } from './need-by'
import styles from './need-by-fields.module.css'

const { Item } = Form
const { Text } = Typography

interface BasisCopy {
  /** The choice as the requester reads it. */
  choice: string
  /** What the choice means, under it. */
  note: string
  /** The one follow-up the basis asks. */
  followUp: string
  example: string
  hint: string
  /** What the preview says the date is, beside it. */
  reads: string
}

/**
 * The form's wording for each basis. The lookup's names are the short labels lists use; asking the
 * question takes more words, and these were settled with the design, so they live here rather than in
 * the API.
 */
export const BASIS_COPY: Record<NeedByBasis, BasisCopy> = {
  [NeedByBasis.Regulatory]: {
    choice: 'A regulation or standard sets it',
    note: 'Missing it means we are not compliant.',
    followUp: 'Which regulation or standard, and the clause?',
    example: 'PCI DSS 4.0, requirement 3.6.1',
    hint: 'Reviewers can check this before planning a quarter around it.',
    reads: 'treated as immovable',
  },
  [NeedByBasis.Contractual]: {
    choice: 'A contract names it',
    note: 'Missing it puts us in breach.',
    followUp: 'Which contract and clause?',
    example: 'Northwind MSA, clause 14.2',
    hint: 'Name the customer and the clause so legal can confirm it.',
    reads: 'treated as immovable',
  },
  [NeedByBasis.ExternalEvent]: {
    choice: 'Something outside us happens then',
    note: 'An end of support, a customer go-live, a season, an event.',
    followUp: 'What is happening on that date?',
    example: 'Northwind finance year end',
    hint: 'The date belongs to the event, so it moves only if the event does.',
    reads: 'treated as immovable',
  },
  [NeedByBasis.Commitment]: {
    choice: 'We promised it to someone',
    note: 'Somebody here gave a date to a customer or a stakeholder.',
    followUp: 'Who did we promise it to?',
    example: 'Northwind Group, in the renewal review',
    hint: 'Recording who promised it means the date can be renegotiated by a person, not just missed.',
    reads: 'can move, by talking to whoever promised it',
  },
  [NeedByBasis.RequesterPreference]: {
    choice: 'It is when the requester would like it',
    note: 'A preference, with nothing riding on it.',
    followUp: 'Anything else about the timing?',
    example: 'Before the next audit cycle starts',
    hint: 'A preference never competes with a fixed date. Saying so here is not a weakness.',
    reads: 'a preference, weighed with everything else',
  },
}

/** What each family means, after its name: the split the whole question turns on. */
const FAMILY_MEANING: Record<NeedByBasisFamily, string> = {
  [NeedByBasisFamily.FixedForUs]: 'something outside this company sets it',
  [NeedByBasisFamily.ChosenByUs]: 'a person here set the date',
}

interface BasisChoiceProps {
  bases: NeedByBasisDto[] | undefined
  families: NeedByBasisFamilyDto[] | undefined
  value?: NeedByBasis
  onChange?: (basis: NeedByBasis) => void
  id?: string
}

/**
 * The bases as two groups of choices, fixed for us and we chose it, so the requester answers "must we, and
 * why that date" in one pick.
 */
const BasisChoice: FC<BasisChoiceProps> = ({
  bases,
  families,
  value,
  onChange,
  id,
}) => (
  <Radio.Group
    id={id}
    value={value}
    onChange={(e) => onChange?.(e.target.value)}
    className={styles.group}
  >
    <Flex vertical gap={16}>
      <Text type="secondary" className={styles.note}>
        A date on its own tells a reviewer nothing. This is the difference
        between a date we cannot move and one we picked.
      </Text>
      {[...(families ?? [])]
        .sort((a, b) => a.order - b.order)
        .map((family) => (
          <Flex vertical gap={8} key={family.code}>
            <Text type="secondary" className={styles.familyHeading}>
              {family.name} — {FAMILY_MEANING[family.code]}
            </Text>
            {(bases ?? [])
              .filter((b) => b.family === family.code)
              .map((b) => (
                <Radio key={b.code} value={b.code} className={styles.choice}>
                  <Flex vertical gap={2}>
                    <Text strong>{BASIS_COPY[b.code].choice}</Text>
                    <Text type="secondary" className={styles.note}>
                      {BASIS_COPY[b.code].note}
                    </Text>
                  </Flex>
                </Radio>
              ))}
          </Flex>
        ))}
    </Flex>
  </Radio.Group>
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
  const form = Form.useFormInstance()
  const employeeOptions = (employees ?? []).map((e) => ({
    label: e.name,
    value: e.id,
  }))

  return (
    <div className={styles.box}>
      <Flex wrap gap={12} align="flex-end" className={styles.dateRow}>
        <Item
          name={['needBy', 'date']}
          label="Does it have to land by a date?"
          className={styles.dateItem}
        >
          <DatePicker allowClear />
        </Item>
        <Button
          className={styles.noDate}
          onClick={() => form.setFieldValue('needBy', {})}
        >
          No particular date
        </Button>
      </Flex>
      <Item
        noStyle
        shouldUpdate={(previous, current) => previous.needBy !== current.needBy}
      >
        {({ getFieldValue }) => {
          const needBy = (getFieldValue('needBy') ?? {}) as NeedByFormValues
          if (!needBy.date) return null
          const basis = bases?.find((b) => b.code === needBy.basis)
          const copy = basis ? BASIS_COPY[basis.code] : undefined

          return (
            <>
              <Item
                name={['needBy', 'basis']}
                label="What makes it that date?"
                rules={[
                  { required: true, message: 'Choose what sets the date.' },
                ]}
              >
                <BasisChoice bases={bases} families={families} />
              </Item>
              {basis && copy && (
                <div className={styles.followUp}>
                  <Item
                    name={['needBy', 'reference']}
                    label={copy.followUp}
                    extra={copy.hint}
                    rules={[
                      {
                        required: true,
                        whitespace: true,
                        message: 'Say what the date rests on.',
                      },
                      { max: 1024 },
                    ]}
                  >
                    <Input
                      maxLength={1024}
                      placeholder={`For example: ${copy.example}`}
                    />
                  </Item>
                  {basis.recordsCommitment && (
                    <Flex wrap gap={12}>
                      <Item
                        name={['needBy', 'committedById']}
                        label="Who made the commitment?"
                        className={styles.half}
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
                        className={styles.half}
                        rules={[
                          {
                            required: true,
                            message: 'Give the date it was made.',
                          },
                        ]}
                      >
                        <DatePicker />
                      </Item>
                    </Flex>
                  )}
                </div>
              )}
              {basis && copy && (
                <Flex wrap gap={8} align="center" className={styles.preview}>
                  <Text type="secondary" className={styles.note}>
                    Reviewers will see
                  </Text>
                  <NeedByTag
                    needBy={{
                      date: toCalendarDate(needBy.date),
                      basis: basis.code,
                      family: basis.family,
                      reference: needBy.reference?.trim() ?? '',
                    }}
                  />
                  <Text type="secondary" className={styles.note}>
                    {copy.reads}
                  </Text>
                </Flex>
              )}
            </>
          )
        }}
      </Item>
    </div>
  )
}
