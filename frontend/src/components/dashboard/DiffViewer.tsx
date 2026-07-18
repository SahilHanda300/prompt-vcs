interface DiffViewerProps {
  before: string
  after: string
  label?: string
}

function diffLines(before: string, after: string) {
  const bLines = before.split('\n')
  const aLines = after.split('\n')
  const result: { type: 'same' | 'removed' | 'added'; text: string }[] = []

  const maxLen = Math.max(bLines.length, aLines.length)
  for (let i = 0; i < maxLen; i++) {
    const b = bLines[i]
    const a = aLines[i]
    if (b === a) {
      if (b !== undefined) result.push({ type: 'same', text: b })
    } else {
      if (b !== undefined) result.push({ type: 'removed', text: b })
      if (a !== undefined) result.push({ type: 'added', text: a })
    }
  }
  return result
}

export function DiffViewer({ before, after, label }: DiffViewerProps) {
  const lines = diffLines(before, after)

  return (
    <div>
      {label && <p className="text-xs text-gray-400 dark:text-slate-400 mb-2">{label}</p>}
      <div className="bg-gray-50 dark:bg-slate-900 rounded border border-gray-200 dark:border-slate-700 overflow-x-auto">
        <pre className="text-xs font-mono p-3 leading-5">
          {lines.map((line, i) => (
            <div
              key={i}
              className={
                line.type === 'removed'
                  ? 'bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-300'
                  : line.type === 'added'
                  ? 'bg-green-50 text-green-700 dark:bg-green-950 dark:text-green-300'
                  : 'text-gray-500 dark:text-slate-400'
              }
            >
              {line.type === 'removed' ? '- ' : line.type === 'added' ? '+ ' : '  '}
              {line.text}
            </div>
          ))}
        </pre>
      </div>
    </div>
  )
}
