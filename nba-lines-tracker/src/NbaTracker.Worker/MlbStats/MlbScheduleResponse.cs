using System.Text.Json.Serialization;

namespace NbaTracker.Worker.Models.MlbStats;

public class MlbScheduleResponse
{
    [JsonPropertyName("dates")] public List<MlbScheduleDate> Dates { get; set; } = [];
}

public class MlbScheduleDate
{
    [JsonPropertyName("date")] public string Date { get; set; } = null!;
    [JsonPropertyName("games")] public List<MlbGame> Games { get; set; } = [];
}

public class MlbGame
{
    [JsonPropertyName("gamePk")]        public int GamePk { get; set; }
    [JsonPropertyName("gameDate")]      public string GameDate { get; set; } = null!;
    [JsonPropertyName("status")]        public MlbGameStatus Status { get; set; } = null!;
    [JsonPropertyName("teams")]         public MlbGameTeams Teams { get; set; } = null!;
    [JsonPropertyName("linescore")]     public MlbLinescore? Linescore { get; set; }
}

public class MlbGameStatus
{
    [JsonPropertyName("abstractGameState")] public string AbstractGameState { get; set; } = null!;
    [JsonPropertyName("detailedState")]     public string DetailedState { get; set; } = null!;
}

public class MlbGameTeams
{
    [JsonPropertyName("home")] public MlbGameTeam Home { get; set; } = null!;
    [JsonPropertyName("away")] public MlbGameTeam Away { get; set; } = null!;
}

public class MlbGameTeam
{
    [JsonPropertyName("team")]  public MlbTeamRef Team { get; set; } = null!;
    [JsonPropertyName("score")] public int? Score { get; set; }
}

public class MlbTeamRef
{
    [JsonPropertyName("id")]   public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
}

public class MlbLinescore
{
    [JsonPropertyName("currentInning")]      public int? CurrentInning { get; set; }
    [JsonPropertyName("currentInningOrdinal")] public string? CurrentInningOrdinal { get; set; }
    [JsonPropertyName("isTopInning")]        public bool? IsTopInning { get; set; }
}

// Separate endpoint: /api/v1/teams?sportId=1
public class MlbTeamsResponse
{
    [JsonPropertyName("teams")] public List<MlbTeamDetail> Teams { get; set; } = [];
}

public class MlbTeamDetail
{
    [JsonPropertyName("id")]           public int Id { get; set; }
    [JsonPropertyName("name")]         public string Name { get; set; } = null!;
    [JsonPropertyName("abbreviation")] public string Abbreviation { get; set; } = null!;
    [JsonPropertyName("league")]       public MlbLeague? League { get; set; }
    [JsonPropertyName("division")]     public MlbDivision? Division { get; set; }
}

public class MlbLeague
{
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
}

public class MlbDivision
{
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
}