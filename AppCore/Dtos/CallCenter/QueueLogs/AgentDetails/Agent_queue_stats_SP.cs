using System.Text.Json.Serialization;

namespace AppCore.Dtos.CallCenter.QueueLogs.AgentDetails
{
    public class Agent_queue_stats_SP
    {
        public string? AgentExtension { get; set; }
        public string? AgentName { get; set; }
        public string? QueueNumber { get; set; }
        public string? QueueName { get; set; }
        public int? UniqueCalls { get; set; }
        public int? NoAnsweredCalls { get; set; }
        public int? AnsweredCalls { get; set; }
        public double? AnswerRate { get; set; }
        public TimeSpan? TotalTalkTime { get; set; }
        public int? TotalSessions { get; set; }
        public TimeSpan? Login_Duration { get; set; }
        public double? AvgTalkTime { get; set; }
        public TimeSpan? LongestTalkTime { get; set; }
        public string? LongestCallId { get; set; }
        public DateTime? LongestCallTime { get; set; }
        public TimeSpan? ShortestTalkTime { get; set; }
        public string? ShortestCallId { get; set; }
        public DateTime? ShortestCallTime { get; set; }
        public int? Count { get; set; }
    }

    public class Agent_queue_stats_SP_Datatable
    {
        public string? AgentExtension { get; set; }
        public string? AgentName { get; set; }
        public List<Agent_queue_stats_SP_Grouped>? Queues { get; set; }
    }

    public class Agent_queue_stats_SP_Grouped
    {
        public string? QueueNumber { get; set; }
        public string? QueueName { get; set; }
        public Agent_calls_stats? Stats { get; set; }
    }

    public class Agent_calls_stats
    {
        public int? UniqueCalls { get; set; }
        public int? NoAnsweredCalls { get; set; }
        public int? AnsweredCalls { get; set; }
        public double? AnswerRate { get; set; }
        public TimeSpan? TotalTalkTime { get; set; }
        public double? AvgTalkTime { get; set; }
        public TimeSpan? LongestTalkTime { get; set; }
        public string? LongestCallId { get; set; }
        public string? LongestCallAt { get; set; }
        public TimeSpan? ShortestTalkTime { get; set; }
        public string? ShortestCallId { get; set; }
        public string? ShortestCallAt{ get; set; }
        public int? TotalSessions { get; set; }
        public TimeSpan? Login_Duration { get; set; }
    }
}
