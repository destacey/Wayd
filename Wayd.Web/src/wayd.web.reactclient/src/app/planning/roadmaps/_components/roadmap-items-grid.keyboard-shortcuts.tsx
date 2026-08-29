import { Descriptions, Flex, Typography } from 'antd'

const { Title, Text } = Typography
const { Item: DescriptionItem } = Descriptions

export const RoadmapItemsHelp = () => (
  <div
    style={{
      width: 400,
      maxWidth: 'calc(100vw - 48px)',
      maxHeight: 'min(70dvh, 500px)',
      overflowY: 'auto',
      overflowX: 'hidden',
      overscrollBehavior: 'contain',
      paddingRight: 4,
    }}
  >
    <Flex vertical gap="large" style={{ width: '100%' }}>
      <Title level={4}>Keyboard Shortcuts</Title>
      <Descriptions
        size="small"
        column={1}
        styles={{ header: { marginBottom: 4 } }}
        title="Inline Editing"
      >
        <DescriptionItem label="Click row or cell">
          <Text>Enter edit mode</Text>
        </DescriptionItem>
        <DescriptionItem label="Enter / Down Arrow">
          <Text>Save and move to next row</Text>
        </DescriptionItem>
        <DescriptionItem label="Up Arrow">
          <Text>Save and move to previous row</Text>
        </DescriptionItem>
        <DescriptionItem label="Tab">
          <Text>Move to next field (wraps to next row)</Text>
        </DescriptionItem>
        <DescriptionItem label="Shift + Tab">
          <Text>Move to previous field (wraps to previous row)</Text>
        </DescriptionItem>
        <DescriptionItem label="Esc">
          <Text>Cancel changes and exit edit mode</Text>
        </DescriptionItem>
        <DescriptionItem label="Click outside table">
          <Text>Save changes and exit edit mode</Text>
        </DescriptionItem>
      </Descriptions>

      <Descriptions
        size="small"
        column={1}
        title="Dropdown and Picker Navigation"
        styles={{ header: { marginBottom: 4 } }}
      >
        <DescriptionItem label="Space">
          <Text>Open dropdown or color picker</Text>
        </DescriptionItem>
        <DescriptionItem label="Up / Down Arrow">
          <Text>Navigate options in open dropdown</Text>
        </DescriptionItem>
        <DescriptionItem label="Enter">
          <Text>Select highlighted option</Text>
        </DescriptionItem>
      </Descriptions>

      <Title level={4}>Drag and Drop</Title>
      <Descriptions
        size="small"
        column={1}
        styles={{ header: { marginBottom: 4 } }}
      >
        <DescriptionItem label="Drag handle">
          <Text>Click and drag the grip icon to move an activity</Text>
        </DescriptionItem>
        <DescriptionItem label="Vertical drag">
          <Text>Move activity up or down in the hierarchy</Text>
        </DescriptionItem>
        <DescriptionItem label="Drag right">
          <Text>Nest activity under the item above it</Text>
        </DescriptionItem>
        <DescriptionItem label="Drag left">
          <Text>Move activity to a shallower level</Text>
        </DescriptionItem>
        <DescriptionItem label="Esc">
          <Text>Cancel drag operation</Text>
        </DescriptionItem>
      </Descriptions>
    </Flex>
  </div>
)

