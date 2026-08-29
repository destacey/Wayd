'use client'

import React, { useState } from 'react'
import { Tree } from 'antd'
import type { TreeDataNode, TreeProps } from 'antd'
import { NodeChangedCallback } from '.'

/**
 * Props for the DraggableTree component.
 *
 * @template T - The type of tree nodes, extending TreeDataNode.
 * @param data - The array of tree nodes to display.
 * @param onNodeChanged - Callback function invoked when a node is moved.
 */
interface DraggableTreeProps<T extends TreeDataNode> {
  data: T[]
  onNodeChanged: NodeChangedCallback
}

const DraggableTree = <T extends TreeDataNode>(
  props: DraggableTreeProps<T>,
) => {
  const [treeData, setTreeData] = useState<T[]>(props.data ?? [])

  //   const onDragEnter: TreeProps['onDragEnter'] = (info) => {
  //     console.log('onDragEnter', info)
  //   }

  const onDrop: TreeProps['onDrop'] = (info) => {
    //console.log('onDrop', info)
    const dropKey = info.node.key
    const dragKey = info.dragNode.key
    const dropPos = info.node.pos.split('-')
    // the drop position relative to the drop node, inside 0, top -1, bottom 1
    const dropPosition = info.dropPosition - Number(dropPos[dropPos.length - 1])

    const loop = (
      data: T[],
      key: React.Key,
      callback: (node: T, i: number, data: T[]) => void,
    ) => {
      for (let i = 0; i < data.length; i++) {
        if (data[i].key === key) {
          return callback(data[i], i, data)
        }
        if (data[i].children) {
          loop(data[i].children as T[], key, callback)
        }
      }
    }

    const dataCopy = [...treeData]

    // Find dragObject
    let dragObj: T | undefined
    loop(dataCopy, dragKey, (item, index, arr) => {
      arr.splice(index, 1)
      dragObj = item
    })

    if (!dragObj) {
      return
    }

    let newParentId: React.Key | null = null
    let newPosition: number = 0

    if (!info.dropToGap) {
      // Drop on the node
      loop(dataCopy, dropKey, (item) => {
        item.children = item.children || []
        item.children.unshift(dragObj!)
        newParentId = item.key
        newPosition = 0 // Since we unshift, position is 0
      })
    } else {
      // Drop on the gap
      let ar: T[] = []
      let i: number = 0
      loop(dataCopy, dropKey, (_item, index, arr) => {
        ar = arr
        i = index
      })
      if (dropPosition === -1) {
        // Drop above the node
        ar.splice(i, 0, dragObj!)
        newParentId = getParentKey(dataCopy, dropKey)
        newPosition = i
      } else {
        // Drop below the node
        ar.splice(i + 1, 0, dragObj!)
        newParentId = getParentKey(dataCopy, dropKey)
        newPosition = i + 1
      }
    }

    setTreeData(dataCopy)

    // Invoke the onNodeChanged callback with the dragged key, new parent ID, and position
    props.onNodeChanged(dragKey, newParentId, newPosition)
  }

  const getParentKey = (data: T[], key: React.Key): React.Key | null => {
    let parentKey: React.Key | null = null

    const traverse = (nodes: T[], parent: T | null) => {
      for (const node of nodes) {
        if (node.key === key) {
          parentKey = parent ? parent.key : null
          return
        }
        if (node.children) {
          traverse(node.children as T[], node)
        }
      }
    }

    traverse(data, null)
    return parentKey
  }

  return (
    <Tree
      draggable
      blockNode
      //onDragEnter={onDragEnter}
      onDrop={onDrop}
      treeData={treeData}
      style={{ padding: '8px' }}
    />
  )
}

export default DraggableTree
