'use client'

import { LabeledContent } from '@/src/components/common/content'
import {
  RecordFactsGroup,
  RecordPersonLink,
} from '@/src/components/common/record'
import { EmployeeDetailsDto } from '@/src/services/wayd-api'
import { useGetDirectReportsQuery } from '@/src/store/features/organizations/employee-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { Divider, Flex, Typography } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'

const { Text } = Typography

const NOT_PROVIDED = 'Not provided'

const Value = ({ children }: { children?: string | null }) =>
  children ? <>{children}</> : <Text type="secondary">{NOT_PROVIDED}</Text>

export interface EmployeeFactsProps {
  employee: EmployeeDetailsDto
}

/**
 * The employee's stable facts, for the details panel.
 *
 * Two groups: what the record is, then who it is connected to. No card or
 * column of its own — the panel supplies the frame, and at mobile widths the
 * same stack renders inline.
 */
const EmployeeFacts = ({ employee }: EmployeeFactsProps) => {
  const { data: directReports } = useGetDirectReportsQuery(employee.id, {
    skip: !employee.id,
  })

  const sortedReports = [...(directReports ?? [])].sort((a, b) =>
    caseInsensitiveCompare(a.displayName, b.displayName),
  )

  // Only the addresses beyond the primary — that one is already shown as
  // Email, and most people have no others, so the row is omitted entirely
  // rather than repeating it or rendering a dash.
  const additionalEmails = (employee.emails ?? [])
    .filter((e) => !e.isPrimary)
    .map((e) => e.email)

  const tenure = employee.hireDate
    ? dayjs().diff(dayjs(employee.hireDate), 'month')
    : null

  const tenureLabel =
    tenure === null
      ? null
      : tenure < 12
        ? `${tenure}m`
        : `${Math.floor(tenure / 12)}y ${tenure % 12}m`

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Email">
          <Link href={`mailto:${employee.email}`}>{employee.email}</Link>
        </LabeledContent>

        {additionalEmails.length > 0 && (
          <LabeledContent label="Additional Emails">
            {additionalEmails.join(', ')}
          </LabeledContent>
        )}

        <LabeledContent label="Employee Number">
          <Value>{employee.employeeNumber}</Value>
        </LabeledContent>

        <LabeledContent label="Employee Type">
          <Value>{employee.employeeType}</Value>
        </LabeledContent>

        <LabeledContent label="Hire Date">
          {employee.hireDate ? (
            <>
              {dayjs(employee.hireDate).format('MMM D, YYYY')}
              {tenureLabel && (
                <Text type="secondary">{` · ${tenureLabel}`}</Text>
              )}
            </>
          ) : (
            <Text type="secondary">{NOT_PROVIDED}</Text>
          )}
        </LabeledContent>

        {/* Job title and department are the header's descriptor line, so the
            panel does not repeat them. */}
        <LabeledContent label="Office Location">
          <Value>{employee.officeLocation}</Value>
        </LabeledContent>
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Manager">
          {employee.manager ? (
            <RecordPersonLink
              name={employee.manager.name}
              href={`/organizations/employees/${employee.manager.key}`}
            />
          ) : (
            <Text type="secondary">No manager assigned</Text>
          )}
        </LabeledContent>

        {/* Most people have none, so an empty row would be noise on the
            majority of records. */}
        {sortedReports.length > 0 && (
          <LabeledContent label="Direct Reports">
            <Flex vertical gap={6}>
              {sortedReports.map((report) => (
                <RecordPersonLink
                  key={report.id}
                  name={report.displayName}
                  href={`/organizations/employees/${report.key}`}
                />
              ))}
            </Flex>
          </LabeledContent>
        )}
      </RecordFactsGroup>
    </>
  )
}

export default EmployeeFacts
