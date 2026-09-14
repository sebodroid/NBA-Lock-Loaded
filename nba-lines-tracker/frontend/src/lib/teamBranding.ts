import type { Sport } from './sports'

export interface TeamBrand {
  abbreviation: string
  name: string
  primary: string
  secondary: string
}

// All 32 NFL teams' primary/secondary brand colors, keyed by the abbreviation the API
// returns (matches Highlightly's team abbreviations). Used for team-colored accents —
// e.g. a matchup card's top border, a favorite-team highlight, or (via palettes.ts,
// which derives from this list) the full-app color palette picker — not the app's
// default chrome, which is its own purple/gold theme.
export const NFL_TEAMS: TeamBrand[] = [
  { abbreviation: 'ARI', name: 'Arizona Cardinals',    primary: '#97233F', secondary: '#000000' },
  { abbreviation: 'ATL', name: 'Atlanta Falcons',      primary: '#A71930', secondary: '#000000' },
  { abbreviation: 'BAL', name: 'Baltimore Ravens',     primary: '#241773', secondary: '#9E7C0C' },
  { abbreviation: 'BUF', name: 'Buffalo Bills',        primary: '#00338D', secondary: '#C60C30' },
  { abbreviation: 'CAR', name: 'Carolina Panthers',    primary: '#0085CA', secondary: '#101820' },
  { abbreviation: 'CHI', name: 'Chicago Bears',        primary: '#0B162A', secondary: '#C83803' },
  { abbreviation: 'CIN', name: 'Cincinnati Bengals',   primary: '#FB4F14', secondary: '#000000' },
  { abbreviation: 'CLE', name: 'Cleveland Browns',     primary: '#311D00', secondary: '#FF3C00' },
  { abbreviation: 'DAL', name: 'Dallas Cowboys',       primary: '#041E42', secondary: '#869397' },
  { abbreviation: 'DEN', name: 'Denver Broncos',       primary: '#FB4F14', secondary: '#002244' },
  { abbreviation: 'DET', name: 'Detroit Lions',        primary: '#0076B6', secondary: '#B0B7BC' },
  { abbreviation: 'GB',  name: 'Green Bay Packers',    primary: '#203731', secondary: '#FFB612' },
  { abbreviation: 'HOU', name: 'Houston Texans',       primary: '#03202F', secondary: '#A71930' },
  { abbreviation: 'IND', name: 'Indianapolis Colts',   primary: '#002C5F', secondary: '#A2AAAD' },
  { abbreviation: 'JAX', name: 'Jacksonville Jaguars', primary: '#101820', secondary: '#D7A22A' },
  { abbreviation: 'KC',  name: 'Kansas City Chiefs',   primary: '#E31837', secondary: '#FFB81C' },
  { abbreviation: 'LAC', name: 'Los Angeles Chargers', primary: '#0080C6', secondary: '#FFC20E' },
  { abbreviation: 'LAR', name: 'Los Angeles Rams',     primary: '#003594', secondary: '#FFA300' },
  { abbreviation: 'LV',  name: 'Las Vegas Raiders',    primary: '#000000', secondary: '#A5ACAF' },
  { abbreviation: 'MIA', name: 'Miami Dolphins',       primary: '#008E97', secondary: '#FC4C02' },
  { abbreviation: 'MIN', name: 'Minnesota Vikings',    primary: '#4F2683', secondary: '#FFC62F' },
  { abbreviation: 'NE',  name: 'New England Patriots', primary: '#002244', secondary: '#C60C30' },
  { abbreviation: 'NO',  name: 'New Orleans Saints',   primary: '#D3BC8D', secondary: '#101820' },
  { abbreviation: 'NYG', name: 'New York Giants',      primary: '#0B2265', secondary: '#A71930' },
  { abbreviation: 'NYJ', name: 'New York Jets',        primary: '#125740', secondary: '#000000' },
  { abbreviation: 'PHI', name: 'Philadelphia Eagles',  primary: '#004C54', secondary: '#A5ACAF' },
  { abbreviation: 'PIT', name: 'Pittsburgh Steelers',  primary: '#FFB612', secondary: '#101820' },
  { abbreviation: 'SF',  name: 'San Francisco 49ers',  primary: '#AA0000', secondary: '#B3995D' },
  { abbreviation: 'SEA', name: 'Seattle Seahawks',     primary: '#002244', secondary: '#69BE28' },
  { abbreviation: 'TB',  name: 'Tampa Bay Buccaneers', primary: '#D50A0A', secondary: '#34302B' },
  { abbreviation: 'TEN', name: 'Tennessee Titans',     primary: '#0C2340', secondary: '#4B92DB' },
  { abbreviation: 'WAS', name: 'Washington Commanders',primary: '#5A1414', secondary: '#FFB612' },
]

const NFL_COLORS: Record<string, TeamBrand> = Object.fromEntries(NFL_TEAMS.map(t => [t.abbreviation, t]))

export function getTeamBrand(sport: Sport, abbreviation: string | null | undefined): TeamBrand | null {
  if (!abbreviation || sport !== 'nfl') return null
  return NFL_COLORS[abbreviation.toUpperCase()] ?? null
}

// ESPN's public team-logo CDN. No auth, widely used for exactly this. A handful of
// abbreviations differ from what our API returns — remapped here; anything else still
// gets a best-effort URL, and <TeamLogo>'s onError hides it if that guess is wrong
// rather than showing a broken-image icon.
const ESPN_ABBR_OVERRIDES: Record<string, string> = {
  WAS: 'wsh',
}

export function getTeamLogoUrl(sport: Sport, abbreviation: string | null | undefined): string | null {
  if (!abbreviation) return null
  const abbr = ESPN_ABBR_OVERRIDES[abbreviation.toUpperCase()] ?? abbreviation.toLowerCase()
  return `https://a.espncdn.com/i/teamlogos/${sport}/500/${abbr}.png`
}
