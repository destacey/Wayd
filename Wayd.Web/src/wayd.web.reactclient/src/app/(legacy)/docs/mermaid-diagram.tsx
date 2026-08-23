'use client'

import { useEffect, useId, useRef, useState } from 'react'
import mermaid from 'mermaid'

mermaid.initialize({
  startOnLoad: false,
  theme: 'dark',
  securityLevel: 'strict',
})

interface MermaidDiagramProps {
  chart: string
}

export default function MermaidDiagram({ chart }: MermaidDiagramProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const id = useId().replace(/:/g, '-')
  const [svg, setSvg] = useState<string>('')

  useEffect(() => {
    const render = async () => {
      try {
        const { svg: renderedSvg } = await mermaid.render(
          `mermaid-${id}`,
          chart,
        )
        setSvg(renderedSvg)
      } catch {
        // If rendering fails, show the raw code
        setSvg('')
      }
    }
    render()
  }, [chart, id])

  if (!svg) {
    return (
      <pre>
        <code>{chart}</code>
      </pre>
    )
  }

  return (
    <div
      ref={containerRef}
      dangerouslySetInnerHTML={{ __html: svg }}
      style={{ textAlign: 'center', margin: '16px 0' }}
    />
  )
}
