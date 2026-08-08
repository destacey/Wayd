import { EmployeeDetailsDto } from '@/src/services/wayd-api'
import { Descriptions } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'

const { Item } = Descriptions

interface EmployeeDetailsProps {
  employee: EmployeeDetailsDto
}

const EmployeeDetails = ({ employee }: EmployeeDetailsProps) => {
  if (!employee) return null

  // Only the addresses beyond the primary — that one is already shown as Email, and most people
  // have no others, so the row is omitted entirely rather than repeating it or rendering a dash.
  const additionalEmails = (employee.emails ?? [])
    .filter((e) => !e.isPrimary)
    .map((e) => e.email)

  return (
    <>
      <Descriptions>
        <Item label="Email">{employee.email}</Item>
        {additionalEmails.length > 0 && (
          <Item label="Additional Emails">
            {additionalEmails.join(', ')}
          </Item>
        )}
        <Item label="Employee Number">{employee.employeeNumber}</Item>
        <Item label="Employee Type">{employee.employeeType || '—'}</Item>
        <Item label="Job Title">{employee.jobTitle}</Item>
        <Item label="Department">{employee.department}</Item>
        <Item label="Manager">
          {employee.manager && (
            <Link href={`/organizations/employees/${employee.manager.key}`}>
              {employee.manager.name}
            </Link>
          )}
        </Item>
        <Item label="Office Location">{employee.officeLocation}</Item>
        <Item label="Hire Date">
          {employee.hireDate && dayjs(employee.hireDate).format('M/D/YYYY')}
        </Item>
      </Descriptions>
    </>
  )
}

export default EmployeeDetails
