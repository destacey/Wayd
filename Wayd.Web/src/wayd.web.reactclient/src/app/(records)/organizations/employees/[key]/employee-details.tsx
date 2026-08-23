import { LabeledContent } from '@/src/components/common/content'
import { EmployeeDetailsDto } from '@/src/services/wayd-api'
import { Card, Col, Divider, Flex, Row, Typography } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'

const { Text } = Typography

const NOT_PROVIDED = 'Not provided'

interface EmployeeDetailsProps {
  employee: EmployeeDetailsDto
}

const Value = ({ children }: { children?: string | null }) =>
  children ? <>{children}</> : <Text type="secondary">{NOT_PROVIDED}</Text>

const EmployeeDetails = ({ employee }: EmployeeDetailsProps) => {
  if (!employee) return null

  // Only the addresses beyond the primary — that one is already shown as Email, and most people
  // have no others, so the row is omitted entirely rather than repeating it or rendering a dash.
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
    <Row gutter={[16, 16]}>
      <Col xs={24} md={9} xxl={6}>
        <Card size="small">
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

            <Divider size="small" />

            <LabeledContent label="Job Title">
              <Value>{employee.jobTitle}</Value>
            </LabeledContent>

            <LabeledContent label="Department">
              <Value>{employee.department}</Value>
            </LabeledContent>

            <LabeledContent label="Office Location">
              <Value>{employee.officeLocation}</Value>
            </LabeledContent>

            <LabeledContent label="Manager">
              {employee.manager ? (
                <Link href={`/organizations/employees/${employee.manager.key}`}>
                  {employee.manager.name}
                </Link>
              ) : (
                <Text type="secondary">No manager assigned</Text>
              )}
            </LabeledContent>
          </Flex>
        </Card>
      </Col>

    </Row>
  )
}

export default EmployeeDetails
