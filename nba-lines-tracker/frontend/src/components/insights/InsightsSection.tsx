import { Skeleton } from '@/components/ui/skeleton'
import { Badge } from '@/components/ui/badge'
import { CollapsibleSection } from '@/components/ui/collapsible-section'
import { useInsights } from '@/api/insights'
import type { InsightResponse } from '@/types/api'

const CATEGORY_LABELS: Record<InsightResponse['category'], string> = {
  'team-ats': 'ATS',
  'team-ou': 'O/U',
  'market-trend': 'Market',
}

// A distinct hue per category — both for the badge and a left accent stripe on the card,
// so a page full of trend cards isn't just gray text on gray borders.
const CATEGORY_STYLES: Record<InsightResponse['category'], { badge: string; border: string }> = {
  'team-ats':      { badge: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400 border-0',       border: 'border-l-blue-500' },
  'team-ou':       { badge: 'bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400 border-0', border: 'border-l-orange-500' },
  'market-trend':  { badge: 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400 border-0', border: 'border-l-purple-500' },
}

export function InsightsSection() {
  const { data: insights = [], isLoading, isError } = useInsights()

  return (
    <CollapsibleSection title="Trends">
      {isLoading && (
        <div className="space-y-2">
          {[0, 1, 2].map(i => (
            <Skeleton key={i} className="h-14 w-full" />
          ))}
        </div>
      )}

      {isError && (
        <p className="text-sm text-muted-foreground italic">Could not load trends.</p>
      )}

      {!isLoading && !isError && insights.length === 0 && (
        <p className="text-sm text-muted-foreground italic">
          No standout trends yet — check back once more games are in the books.
        </p>
      )}

      {!isLoading && !isError && insights.length > 0 && (
        <div className="grid gap-2 sm:grid-cols-2">
          {insights.map((insight, i) => (
            <div
              key={i}
              className={`flex items-start gap-2 rounded-md border border-l-4 p-3 text-sm ${CATEGORY_STYLES[insight.category].border}`}
            >
              <Badge variant="outline" className={`text-[10px] shrink-0 mt-0.5 ${CATEGORY_STYLES[insight.category].badge}`}>
                {CATEGORY_LABELS[insight.category]}
              </Badge>
              <div>
                <p>{insight.text}</p>
                <p className="text-[11px] text-muted-foreground mt-0.5">
                  Based on {insight.sampleSize} game{insight.sampleSize === 1 ? '' : 's'}
                </p>
              </div>
            </div>
          ))}
        </div>
      )}
    </CollapsibleSection>
  )
}
