import { Skeleton } from '@/components/ui/skeleton'
import { Card, CardContent } from '@/components/ui/card'
import { useAccuracy, type AccuracyBucket } from '@/api/accuracy'

// Calibration color is NOT the same rule as AtsCell's 50%-midpoint green/red — every
// bucket here starts at 65%+ by construction (nothing weaker ever gets snapshotted), so
// a 50%-centered rule would paint everything green regardless of whether the bucket
// actually hits what its own label promises. This compares the bucket's real hit rate
// against its own lower bound instead — that's the actual question being asked here.
function calibrationStyle(bucket: AccuracyBucket): string {
  if (bucket.hitRatePct === null) return 'text-muted-foreground'
  const lo = parseInt(bucket.range, 10)
  const gap = bucket.hitRatePct - lo
  if (gap >= 0) return 'text-green-600 dark:text-green-400'
  if (gap >= -10) return 'text-yellow-600 dark:text-yellow-400'
  return 'text-red-600 dark:text-red-400'
}

function BucketBar({ bucket }: { bucket: AccuracyBucket }) {
  const lo = parseInt(bucket.range, 10)
  const widthPct = bucket.hitRatePct ?? 0

  return (
    <div className="space-y-1">
      <div className="flex items-baseline justify-between text-sm">
        <span className="font-medium">{bucket.range} called</span>
        <span className={`font-semibold tabular-nums ${calibrationStyle(bucket)}`}>
          {bucket.hitRatePct !== null ? `${bucket.hitRatePct}% actual` : 'not enough graded yet'}
        </span>
      </div>
      <div
        className="h-2 rounded-full bg-muted overflow-hidden relative"
        title={`${bucket.hits} of ${bucket.graded} graded picks hit`}
      >
        {/* Target zone: where the bar SHOULD end up if this bucket is honest */}
        <div
          className="absolute inset-y-0 bg-foreground/10"
          style={{ left: `${lo}%`, right: 0 }}
        />
        <div
          className="h-full rounded-full bg-primary transition-all"
          style={{ width: `${Math.min(widthPct, 100)}%` }}
        />
      </div>
      <p className="text-[11px] text-muted-foreground">
        {bucket.graded} graded{bucket.graded > 0 && ` · ${bucket.hits} hit`}
      </p>
    </div>
  )
}

export function AccuracyPage() {
  const { data, isLoading, isError } = useAccuracy()

  return (
    <div className="max-w-lg">
      <p className="text-sm text-muted-foreground mb-4">
        Every Hot Bets pick gets recorded the moment it first qualifies — before the
        outcome is known — then graded automatically once the game ends. This is the
        honest check on whether the confidence numbers mean anything: if the "85-95%"
        bucket isn't actually landing somewhere around 85-95%, that's the thresholds
        being wrong, not bad luck. Early on, every bucket will have too few graded picks
        to mean much — this gets more trustworthy every week.
      </p>

      {isLoading && (
        <div className="space-y-3">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-16 w-full" />)}
        </div>
      )}

      {isError && <p className="text-sm text-muted-foreground italic">Could not load accuracy data.</p>}

      {data && (
        <>
          <Card className="mb-4">
            <CardContent className="p-4 grid grid-cols-3 gap-3 text-center">
              <div>
                <p className="text-2xl font-bold">{data.overallHitRatePct ?? '–'}{data.overallHitRatePct !== null && '%'}</p>
                <p className="text-[11px] text-muted-foreground">Overall hit rate</p>
              </div>
              <div>
                <p className="text-2xl font-bold">{data.gradedSnapshots}</p>
                <p className="text-[11px] text-muted-foreground">Graded picks</p>
              </div>
              <div>
                <p className="text-2xl font-bold">{data.pendingSnapshots}</p>
                <p className="text-[11px] text-muted-foreground">Still pending</p>
              </div>
            </CardContent>
          </Card>

          {data.totalSnapshots === 0 ? (
            <p className="text-sm text-muted-foreground italic">
              No picks recorded yet — this starts filling in the next time Hot Bets runs.
            </p>
          ) : (
            <Card>
              <CardContent className="p-4 space-y-4">
                {data.buckets.map(b => <BucketBar key={b.range} bucket={b} />)}
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  )
}
