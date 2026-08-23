'use client'

import { useEffect, useRef, useState } from 'react'
import styles from './diagnostic-canvas.module.css'

interface MatrixTransitionProps {
  duration?: number
  onComplete: () => void
}

const CHARS = 'アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン0123456789ABCDEF'

export default function MatrixTransition({ duration = 4000, onComplete }: MatrixTransitionProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const [fading, setFading] = useState(false)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const ctx = canvas.getContext('2d')
    if (!ctx) return

    canvas.width = window.innerWidth
    canvas.height = window.innerHeight

    const fontSize = 14
    const columns = Math.floor(canvas.width / fontSize)
    const drops: number[] = Array(columns).fill(0).map(() => Math.random() * -50)

    let animFrame: number

    const draw = () => {
      ctx.fillStyle = 'rgba(0, 0, 0, 0.05)'
      ctx.fillRect(0, 0, canvas.width, canvas.height)

      ctx.font = `${fontSize}px monospace`

      for (let i = 0; i < drops.length; i++) {
        const char = CHARS[Math.floor(Math.random() * CHARS.length)]
        const x = i * fontSize
        const y = drops[i] * fontSize

        // Head of the column is brighter
        const brightness = Math.random() > 0.98 ? '#fff' : Math.random() > 0.5 ? '#4ade80' : '#166534'
        ctx.fillStyle = brightness
        ctx.fillText(char, x, y)

        if (y > canvas.height && Math.random() > 0.975) {
          drops[i] = 0
        }

        drops[i] += 0.5 + Math.random() * 0.5
      }

      animFrame = requestAnimationFrame(draw)
    }

    animFrame = requestAnimationFrame(draw)

    // Start fade out before completing
    const fadeTimer = setTimeout(() => setFading(true), duration - 800)
    const completeTimer = setTimeout(onComplete, duration)

    const handleResize = () => {
      canvas.width = window.innerWidth
      canvas.height = window.innerHeight
    }
    window.addEventListener('resize', handleResize)

    return () => {
      cancelAnimationFrame(animFrame)
      clearTimeout(fadeTimer)
      clearTimeout(completeTimer)
      window.removeEventListener('resize', handleResize)
    }
  }, [duration, onComplete])

  return (
    <canvas
      ref={canvasRef}
      className={`${styles.matrixCanvas} ${fading ? styles.matrixFadeOut : ''}`}
    />
  )
}
