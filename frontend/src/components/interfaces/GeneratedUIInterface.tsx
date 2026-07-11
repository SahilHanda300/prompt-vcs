import type { SiteListItem } from '../../types'

interface Props {
  site: SiteListItem
}

export function GeneratedUIInterface({ site }: Props) {
  return (
    <div className="flex flex-col h-full">
      <div className="px-6 py-3 border-b border-white/5 flex items-center justify-between shrink-0">
        <div>
          <p className="text-white font-medium text-sm">{site.refname}</p>
          <p className="text-slate-600 text-xs">{site.commitmessage || 'Generated UI'}</p>
        </div>
        <span className="text-[10px] text-slate-600 uppercase tracking-widest">generated-ui</span>
      </div>
      <iframe
        srcDoc={site.systemtemplate}
        sandbox="allow-scripts allow-forms"
        className="flex-1 w-full border-0"
        title={site.refname}
      />
    </div>
  )
}
