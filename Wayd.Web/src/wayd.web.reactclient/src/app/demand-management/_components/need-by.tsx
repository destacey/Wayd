'use client'

import {
  IntakeRequestNeedByDto,
  IntakeRequestNeedByRequest,
  NeedByBasis,
  NeedByBasisDto,
  NeedByBasisFamily,
} from '@/src/services/wayd-api'
import {
  formatCalendarDate,
  parseCalendarDate,
  toCalendarDate,
} from '@/src/utils'
import { Flex, Tag, Typography } from 'antd'
import type { Dayjs } from 'dayjs'
import type { NamePath } from 'antd/es/form/interface'
import { FC } from 'react'
import styles from './need-by.module.css'

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

/**
 * A need-by in a sentence, as the request page tells it: what makes the date, and what it rests on. A
 * commitment also says who made it and when, since that is who could renegotiate it.
 */
export const needByExplanation = (needBy: IntakeRequestNeedByDto): string => {
  const ref = needBy.reference.trim()
  const rest = ref ? ` — ${ref}.` : '.'
  switch (needBy.basis) {
    case NeedByBasis.Regulatory:
      return `A regulation or standard sets it${rest}`
    case NeedByBasis.Contractual:
      return `A contract names it${rest}`
    case NeedByBasis.ExternalEvent:
      return `Something outside us happens then${rest}`
    case NeedByBasis.Commitment: {
      const who = needBy.committedBy?.name ?? 'someone here'
      const to = ref ? ` to ${ref}` : ''
      const when = needBy.committedOn
        ? ` on ${formatCalendarDate(needBy.committedOn)}`
        : ''
      return `A commitment we made — ${who} promised it${to}${when}.`
    }
    default:
      return `When the requester would like it${rest}`
  }
}

/** Whether the date can move, and how: the answer a reviewer planning around it most needs. */
export const needByCanMove = (needBy: IntakeRequestNeedByDto): string => {
  switch (needBy.basis) {
    case NeedByBasis.Regulatory:
      return 'Only if the regulation changes'
    case NeedByBasis.Contractual:
      return 'Only if the contract changes'
    case NeedByBasis.ExternalEvent:
      return 'Only if the event moves'
    case NeedByBasis.Commitment:
      return needBy.committedBy
        ? `Yes, by talking to ${needBy.committedBy.name}`
        : 'Yes, by talking to whoever promised it'
    default:
      return 'Yes, it is a preference'
  }
}

/**
 * The need-by on the request page: the date, what makes it that date, and whether it can move, coloured as
 * the list colours it so a fixed date reads as one at a glance.
 */
export const NeedByCallout: FC<{ needBy: IntakeRequestNeedByDto }> = ({
  needBy,
}) => {
  const tone =
    needBy.family === NeedByBasisFamily.FixedForUs
      ? styles.fixed
      : needBy.basis === NeedByBasis.Commitment
        ? styles.promised
        : ''
  return (
    <div className={`${styles.callout} ${tone}`}>
      <Flex vertical gap={2} className={styles.what}>
        <Text strong className={styles.date}>
          {formatCalendarDate(needBy.date, 'MMMM D, YYYY')}
        </Text>
        <Text type="secondary">{needByExplanation(needBy)}</Text>
      </Flex>
      <Flex vertical gap={2} className={styles.move}>
        <Text type="secondary" className={styles.question}>
          Can it move?
        </Text>
        <Text>{needByCanMove(needBy)}</Text>
      </Flex>
    </div>
  )
}
