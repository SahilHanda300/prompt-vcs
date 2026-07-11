interface ScoreBadgeProps {
  score: number | null
  type: 'golden' | 'judge'
}

export function ScoreBadge({ score, type }: ScoreBadgeProps) {
  if (score === null || score === undefined) {
    return <span className="px-2 py-0.5 rounded text-xs bg-slate-700 text-slate-400">—</span>
  }

  let colour: string
  let label: string

  if (type === 'golden') {
    label = score.toFixed(2)
    colour = score >= 0.85 ? 'bg-green-900 text-green-300' : score >= 0.70 ? 'bg-amber-900 text-amber-300' : 'bg-red-900 text-red-300'
  } else {
    label = `${score.toFixed(1)}/5`
    colour = score >= 4.0 ? 'bg-green-900 text-green-300' : score >= 3.0 ? 'bg-amber-900 text-amber-300' : 'bg-red-900 text-red-300'
  }

  return (
    <span className={`px-2 py-0.5 rounded text-xs font-mono font-medium ${colour}`}>
      {label}
    </span>
  )
}
