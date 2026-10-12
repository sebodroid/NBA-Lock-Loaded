import { CloudRain, Snowflake } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import type { GameWeatherSummary } from '@/types/api'

// Renders nothing for indoor/retractable-roof venues (weather is null) AND for outdoor
// games with benign conditions — a badge only appears when it's actually worth a
// bettor's attention, same "missing = fine" convention as InjuryBadge.
const RISK_PRECIP_PCT = 40

export function WeatherBadge({ weather }: { weather: GameWeatherSummary | null }) {
  if (!weather) return null

  const forecast = weather.shortForecast?.toLowerCase() ?? ''
  const isSnow = forecast.includes('snow')
  const isRainRisk = (weather.precipitationChancePct ?? 0) >= RISK_PRECIP_PCT
    || forecast.includes('rain') || forecast.includes('storm')

  if (!isSnow && !isRainRisk) return null

  const label = [
    weather.shortForecast,
    weather.precipitationChancePct !== null ? `${weather.precipitationChancePct}%` : null,
    weather.temperatureF !== null ? `${weather.temperatureF}°F` : null,
    weather.windSpeed,
  ].filter(Boolean).join(' · ')

  return (
    <Badge
      variant="outline"
      title={label}
      className={`text-[10px] px-1 py-0 shrink-0 gap-0.5 border-0 ${
        isSnow
          ? 'bg-sky-100 text-sky-800 dark:bg-sky-900/30 dark:text-sky-400'
          : 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400'
      }`}
    >
      {isSnow ? <Snowflake className="h-2.5 w-2.5" /> : <CloudRain className="h-2.5 w-2.5" />}
      {weather.precipitationChancePct !== null ? `${weather.precipitationChancePct}%` : 'Weather'}
    </Badge>
  )
}
