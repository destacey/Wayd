import { Typography } from 'antd'
import { Fragment } from 'react'

const { Text } = Typography

/**
 * An import's or a column's description as the API publishes it: plain text in which `backticks` mark a
 * column name or a literal value, shown as code.
 */
export const ImportDescription = ({ text }: { text: string }) => (
  <>
    {text.split(/`([^`]+)`/).map((part, index) =>
      index % 2 === 1 ? (
        <Text code key={index}>
          {part}
        </Text>
      ) : (
        <Fragment key={index}>{part}</Fragment>
      ),
    )}
  </>
)
