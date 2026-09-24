using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class Agent_DetailsTableAndSP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agent_queue_stats_SP",
                columns: table => new
                {
                    AgentExtension = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QueueNumber = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QueueName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UniqueCalls = table.Column<int>(type: "int", nullable: true),
                    NoAnsweredCalls = table.Column<int>(type: "int", nullable: true),
                    AnsweredCalls = table.Column<int>(type: "int", nullable: true),
                    AnswerRate = table.Column<double>(type: "double", nullable: true),
                    TotalTalkTime = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    TotalSessions = table.Column<int>(type: "int", nullable: true),
                    Login_Duration = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    AvgTalkTime = table.Column<double>(type: "double", nullable: true),
                    LongestTalkTime = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    LongestCallId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LongestCallTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ShortestTalkTime = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    ShortestCallId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShortestCallTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Count = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            string Get_agents_statistics = 
            @"
            
            DROP PROCEDURE IF EXISTS Get_agents_statistics;
            CREATE DEFINER=`root`@`localhost` PROCEDURE `Get_agents_statistics`(
                IN date_from TIMESTAMP,
                IN date_to TIMESTAMP,
                IN take_count INT,
                IN skip_count INT,
                IN p_agent VARCHAR(255),
                IN p_extension VARCHAR(255),
                IN p_queueNum VARCHAR(255),
                IN p_queueName VARCHAR(255),
                IN roleId VARCHAR(255)
            )
            BEGIN

            WITH allCalls AS (
                SELECT
                    ql.agent,
                    COALESCE(ql.data4, '') AS AgentName,
                    COALESCE(ql.queuename, '') AS QueueNumber,
                    COALESCE(ql.data5, '') AS QueueName,
                    ql.callid,
                    ql.event,
                    ql.time,
                    CAST(ql.data2 AS UNSIGNED) AS talktime
                FROM Queue_logs ql
                WHERE ql.agent IS NOT NULL
                AND ql.agent <> 'NONE'
                AND ql.time BETWEEN date_from AND date_to
                AND (p_agent IS NULL OR ql.data4 = p_agent)
                AND (p_extension IS NULL OR ql.agent = CONCAT('PJSIP/', p_extension))
                AND (p_queueNum IS NULL OR ql.queuename = p_queueNum)
                AND (p_queueName IS NULL OR ql.data5 = p_queueName)
                AND ql.event IN (
                    'COMPLETECALLER',
                    'COMPLETEAGENT',
                    'ATTENDEDTRANSFER',
                    'BLINDTRANSFER',
                    'RINGNOANSWER',
                    'ADDMEMBER',
                    'REMOVEMEMBER'
                )
                AND (roleId IS NULL
                        OR EXISTS (
                            SELECT 1
                            FROM QueuePortals portals
                            WHERE portals.QueuePortal = ql.queuename
                            AND portals.RoleId = roleId
                        ))
            ),

            answered_calls AS (
                SELECT *
                FROM allCalls
                WHERE event IN (
                    'COMPLETECALLER',
                    'COMPLETEAGENT',
                    'ATTENDEDTRANSFER',
                    'BLINDTRANSFER'
                )
            ),


            noAnswerAgg AS (
                SELECT
                    agent,
                    AgentName,
                    QueueNumber,
                    QueueName,
                    COUNT(callid) AS NoAnsweredCalls
                FROM allCalls
                WHERE event = 'RINGNOANSWER'
                GROUP BY agent, AgentName, QueueNumber, QueueName
            ),

            login_events AS (
                SELECT
                    agent,
                    AgentName,
                    QueueNumber,
                    QueueName,
                    event,
                    time
                FROM allCalls
                WHERE event IN ('ADDMEMBER','REMOVEMEMBER')
            ),

            adds AS (
                SELECT agent, AgentName, QueueName , QueueNumber, time AS login_time,
                    ROW_NUMBER() OVER (PARTITION BY agent, QueueName, AgentName, QueueNumber ORDER BY time) AS rn
                FROM login_events
                WHERE event = 'ADDMEMBER'
            ),

            removes AS (
                SELECT agent, AgentName , QueueName , QueueNumber, time AS logout_time,
                    ROW_NUMBER() OVER (PARTITION BY agent, QueueName, AgentName, QueueNumber ORDER BY time) AS rn
                FROM login_events
                WHERE event = 'REMOVEMEMBER'
            ),

            sessions AS (
                SELECT
                    ad.agent,
                    ad.AgentName,
                    ad.QueueName,
                    ad.QueueNumber,
                    COUNT(*) AS totalSessions,
                    SUM(TIMESTAMPDIFF(SECOND, ad.login_time, r.logout_time)) AS login_seconds
                FROM adds ad
                JOIN removes r
                    ON r.agent = ad.agent
                    AND r.AgentName = ad.AgentName
                    AND r.QueueName = ad.QueueName
                    AND r.QueueNumber = ad.QueueNumber
                    AND r.rn = ad.rn
                GROUP BY ad.agent, ad.AgentName, ad.QueueName, ad.QueueNumber
            ),

            agg AS (
                SELECT
                    agent, AgentName, QueueNumber, QueueName,
                    COUNT(DISTINCT callid) AS AnsweredCalls,
                    SUM(talktime)          AS TotalTalkTime,
                    (SUM(talktime) / COUNT(DISTINCT callid))          AS AvgTalkTime,
                    MAX(talktime)          AS LongestTalkTime,
                    MIN(talktime)          AS ShortestTalkTime
                FROM answered_calls
                GROUP BY agent, AgentName, QueueNumber, QueueName
            ),

            ranked AS (
                SELECT
                    agent, AgentName, QueueName, QueueNumber, callid, time, talktime,
                    ROW_NUMBER() OVER (PARTITION BY agent, AgentName, QueueName, QueueNumber ORDER BY talktime DESC, time ASC) AS rn_long,
                    ROW_NUMBER() OVER (PARTITION BY agent, AgentName, QueueName, QueueNumber ORDER BY talktime ASC,  time ASC) AS rn_short
                FROM answered_calls
            ) ,

            allCallsAgg AS (
                SELECT
                    agent,
                    AgentName,
                    QueueNumber,
                    QueueName,
                    COUNT(DISTINCT IF(event NOT IN ('ADDMEMBER', 'REMOVEMEMBER'), callid, NULL)) AS UniqueCalls
                FROM allCalls
                GROUP BY agent, AgentName, QueueNumber, QueueName
            ) ,

            totalAgents AS (
                SELECT COUNT(DISTINCT agent, AgentName) AS Count
                FROM allCalls
            ) ,

            agent_page AS (
                SELECT agent, AgentName
                FROM allCalls
                GROUP BY agent, AgentName
                ORDER BY agent, AgentName
                LIMIT take_count OFFSET skip_count
            )

            SELECT
                allAgg.agent AS AgentExtension,
                allAgg.AgentName,
                allAgg.QueueNumber,
                allAgg.QueueName,
                allAgg.UniqueCalls,
                COALESCE(a.AnsweredCalls, 0) AS AnsweredCalls,
                COALESCE(na.NoAnsweredCalls, 0) AS NoAnsweredCalls,
                COALESCE((COALESCE(a.AnsweredCalls, 0) / (COALESCE(a.AnsweredCalls, 0) + COALESCE(na.NoAnsweredCalls, 0))), 0) AS AnswerRate,
                SEC_TO_TIME(COALESCE(a.TotalTalkTime, 0)) AS TotalTalkTime,
                COALESCE(a.AvgTalkTime, 0) AS AvgTalkTime,
                SEC_TO_TIME(COALESCE(a.LongestTalkTime, 0)) AS LongestTalkTime,
                SEC_TO_TIME(COALESCE(a.ShortestTalkTime, 0)) AS ShortestTalkTime,
                COALESCE(rl.callid, '') AS LongestCallId,
                rl.time   AS LongestCallTime,
                COALESCE(rs.callid, '') AS ShortestCallId,
                rs.time   AS ShortestCallTime,
                COALESCE(s.totalSessions, 0) AS totalSessions,
                COALESCE(s.login_seconds, 0) AS login_seconds,
                SEC_TO_TIME(COALESCE(s.login_seconds, 0)) AS login_duration,
                ta.count
            FROM agent_page ap
            CROSS JOIN totalAgents ta
            JOIN allCallsAgg allAgg  ON allAgg.agent = ap.agent AND allAgg.AgentName = ap.AgentName
            LEFT JOIN agg a          ON a.agent = allAgg.agent AND a.QueueName = allAgg.QueueName AND a.QueueNumber = allAgg.QueueNumber AND a.AgentName = allAgg.AgentName
            LEFT JOIN noAnswerAgg na ON na.agent = allAgg.agent AND na.QueueName = allAgg.QueueName AND na.QueueNumber = allAgg.QueueNumber AND na.AgentName = allAgg.AgentName
            LEFT JOIN ranked rl      ON rl.agent = allAgg.agent AND rl.QueueName = allAgg.QueueName AND rl.QueueNumber = allAgg.QueueNumber AND rl.AgentName = allAgg.AgentName AND rl.rn_long = 1
            LEFT JOIN ranked rs      ON rs.agent = allAgg.agent AND rs.QueueName = allAgg.QueueName AND rs.QueueNumber = allAgg.QueueNumber AND rs.AgentName = allAgg.AgentName AND rs.rn_short = 1
            LEFT JOIN sessions s     ON s.agent = allAgg.agent AND s.QueueName = allAgg.QueueName AND s.QueueNumber = allAgg.QueueNumber AND s.AgentName = allAgg.AgentName
            ORDER BY allAgg.agent, allAgg.AgentName;
            END
            
            ";

            migrationBuilder.Sql(Get_agents_statistics);

            string Get_call_details_By_Id =
            @"
            DROP PROCEDURE IF EXISTS Get_call_details_By_Id;
            CREATE DEFINER=`root`@`localhost` PROCEDURE `Get_call_details_By_Id`(IN p_callid VARCHAR(100)) BEGIN
            SELECT ql.time AS dateAndTime,
                ql.queuename AS qNum,
                ql.data5 AS qName,
                ql.agent AS agent,
                MAX(ql.data4) AS agentName,
                MAX(enterqueue.data2) AS callerId,
                MAX(enterqueue.data3) AS position,
                MAX(cdr.Recording_FileName) AS recording_file,
                MAX(IF(ql.event IN ('ENTERQUEUE', 'RINGCANCELED', 'RINGNOANSWER', 'ABANDON', 'EXITWITHTIMEOUT', 'COMPLETECALLER', 'COMPLETEAGENT', 'ATTENDEDTRANSFER', 'BLINDTRANSFER'), ql.event, NULL)) AS status,
                MAX(IF(ql.event IN ('ABANDON', 'EXITWITHTIMEOUT'), ql.data1, NULL)) AS lastPosition,
                MAX(IF(ql.event IN ('COMPLETECALLER', 'COMPLETEAGENT', 'ATTENDEDTRANSFER', 'BLINDTRANSFER'), CAST(ql.data2 AS UNSIGNED), NULL)) AS duration,
                MAX(IF(ql.event IN ('COMPLETECALLER', 'COMPLETEAGENT', 'ATTENDEDTRANSFER', 'BLINDTRANSFER'), ql.event, NULL)) AS hangUpBy,
                COALESCE(MAX(IF(ql.event = 'RINGNOANSWER', CAST(ql.data1 AS UNSIGNED), NULL)), MAX(IF(ql.event IN ('ABANDON', 'EXITWITHTIMEOUT'), CAST(ql.data3 AS UNSIGNED), NULL)), MAX(IF(ql.event = 'RINGCANCELED', CAST(ql.data1 AS UNSIGNED), NULL)), MAX(IF(ql.event IN ('COMPLETECALLER', 'COMPLETEAGENT', 'ATTENDEDTRANSFER', 'BLINDTRANSFER'), CAST(connect.data1 AS UNSIGNED), NULL)), 0) AS waitingTime
            FROM Queue_logs AS ql
            LEFT JOIN Queue_logs AS enterqueue ON ql.callid = enterqueue.callid
            AND enterqueue.event = 'ENTERQUEUE'
            LEFT JOIN Queue_logs AS connect ON ql.callid = connect.callid
            AND connect.event = 'CONNECT'
            LEFT JOIN CDRs AS cdr ON cdr.Uniqueid = ql.callid
            AND ql.event IN ('COMPLETECALLER',
                            'COMPLETEAGENT',
                            'ATTENDEDTRANSFER',
                            'BLINDTRANSFER')
            WHERE ql.callid = p_callid
                AND ql.event IN ('ENTERQUEUE', 'RINGNOANSWER', 'RINGCANCELED', 'ABANDON', 'EXITWITHTIMEOUT', 'COMPLETECALLER', 'COMPLETEAGENT', 'ATTENDEDTRANSFER', 'BLINDTRANSFER')

            GROUP BY ql.time, ql.event, ql.agent, ql.callid, ql.queuename, ql.data4, ql.data5
            ORDER BY ql.time DESC; 
            END
            ";

            migrationBuilder.Sql(Get_call_details_By_Id);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Agent_queue_stats_SP");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS Get_agents_statistics;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS Get_call_details_By_Id;");
        }
    }
}
