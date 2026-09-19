import { Select } from 'antd'
import { BaseOptionType } from 'antd/es/select'

interface EmployeeSelectProps {
  employees: BaseOptionType[]
  allowMultiple?: boolean
  placeholder?: string
  id?: string // required by antd for custom form controls
  value?: string | string[] // required by antd for custom form controls
  onChange?: (value: string | string[]) => void // required by antd for custom form controls
}

const EmployeeSelect: React.FC<EmployeeSelectProps> = ({
  employees,
  allowMultiple,
  placeholder,
  id,
  value,
  onChange,
}) => {
  const selectPlaceholder =
    placeholder ??
    (allowMultiple ? 'Select one or more employees' : 'Select an employee')

  return (
    <span id={id}>
      <Select
        mode={allowMultiple ? 'multiple' : undefined}
        allowClear
        placeholder={selectPlaceholder}
        showSearch={{
          optionFilterProp: 'label',
          filterOption: (input, option) =>
            (option?.label?.toLowerCase() ?? '').includes(input.toLowerCase()),
        }}
        options={employees}
        value={value}
        onChange={onChange}
      />
    </span>
  )
}

export default EmployeeSelect
