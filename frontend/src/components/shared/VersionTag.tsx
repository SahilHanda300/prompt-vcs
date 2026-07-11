interface VersionTagProps {
  hash: string
}

export function VersionTag({ hash }: VersionTagProps) {
  return (
    <span className="px-2 py-0.5 rounded text-xs font-mono bg-slate-700 text-slate-300">
      {hash.slice(0, 8)}
    </span>
  )
}
