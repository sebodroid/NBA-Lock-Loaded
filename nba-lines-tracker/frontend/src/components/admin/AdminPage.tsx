import { formatDistanceToNow } from 'date-fns'
import { Check, X } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { usePendingUsers, useApproveUser, useRejectUser } from '@/api/admin'

export function AdminPage() {
  const { data: pending = [], isLoading, isError } = usePendingUsers()
  const approve = useApproveUser()
  const reject = useRejectUser()

  return (
    <div>
      <p className="text-sm text-muted-foreground mb-4">
        Accounts waiting for approval before they can sign in.
      </p>

      {isLoading && (
        <div className="grid gap-3 sm:grid-cols-2">
          {[0, 1].map(i => <Skeleton key={i} className="h-20 w-full" />)}
        </div>
      )}

      {isError && <p className="text-sm text-muted-foreground italic">Could not load pending accounts.</p>}

      {!isLoading && !isError && pending.length === 0 && (
        <p className="text-sm text-muted-foreground italic">No pending accounts.</p>
      )}

      {!isLoading && !isError && pending.length > 0 && (
        <div className="grid gap-3 sm:grid-cols-2">
          {pending.map(u => (
            <Card key={u.id}>
              <CardContent className="p-4 flex items-center justify-between gap-3">
                <div className="min-w-0">
                  <p className="text-sm font-semibold truncate">{u.username}</p>
                  <p className="text-xs text-muted-foreground truncate">{u.email}</p>
                  <p className="text-xs text-muted-foreground">
                    Requested {formatDistanceToNow(new Date(u.createdAt), { addSuffix: true })}
                  </p>
                </div>
                <div className="flex gap-1.5 shrink-0">
                  <Button
                    size="icon"
                    variant="outline"
                    className="h-8 w-8 text-green-600 dark:text-green-400"
                    onClick={() => approve.mutate(u.id)}
                    disabled={approve.isPending || reject.isPending}
                    aria-label="Approve"
                  >
                    <Check className="h-4 w-4" />
                  </Button>
                  <Button
                    size="icon"
                    variant="outline"
                    className="h-8 w-8 text-red-600 dark:text-red-400"
                    onClick={() => reject.mutate(u.id)}
                    disabled={approve.isPending || reject.isPending}
                    aria-label="Reject"
                  >
                    <X className="h-4 w-4" />
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
