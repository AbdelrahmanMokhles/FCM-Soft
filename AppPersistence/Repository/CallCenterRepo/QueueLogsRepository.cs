using System.Data;
using System.Globalization;
using AppCore.Dtos.CallCenter.QueueLogs.AgentDetails;
using AppCore.Dtos.QueueLogs;
using AppCore.Dtos.QueueLogs.CallDetails;
using AppCore.Dtos.QueueLogs.CallsStatistics;
using AppCore.Dtos.QueueLogs.LoginingAgents;
using AppCore.Dtos.QueueLogs.PauseingAgents;
using AppCore.Dtos.QueueLogs.QueueCallsDetails;
using AppCore.Dtos.QueueLogs.QueueDetails;
using AppCore.Dtos.QueueLogs.Statistics;
using AppCore.HandleResponse;
using AppCore.Interfaces.CallCenter;
using AppDomain.CallCenter;
using AppPersistence.Context;
using AutoMapper;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace AppPersistence.Repository.CallCenterRepo
{
    public class QueueLogsRepository(MyDbContext myDbContext, IMapper mapper) : IQueueLogs
    {
        private readonly MyDbContext _myDbContext = myDbContext;
        private readonly IMapper _mapper = mapper;

        // public async Task<MemoryStream> ExportCSVFileAsync(IEnumerable<object> records)
        // {
        //     using var stream = new MemoryStream();
        //     using var writer = new StreamWriter(stream);

        //     using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        //     if (records.GetType() == typeof(PauseingAgents_Datatable))
        //     {
        //         string? previousUniqueNum = null;
        //         foreach (var record in records.Cast<PauseingAgents_Datatable>())
        //         {
        //             if (previousUniqueNum != null && previousUniqueNum.ToString() != record.AgentNumber)
        //                 // Insert blank row
        //                 await csv.NextRecordAsync();

        //             csv.WriteRecord(record);
        //             await csv.NextRecordAsync();

        //             previousUniqueNum = record.AgentNumber;
        //         }
        //     }

        //     await csv.WriteRecordsAsync(records);

        //     await writer.FlushAsync();

        //     return stream.ToArray();
        // }

        public async Task<MemoryStream> ExportCSVFileAsync(IEnumerable<object> records)
        {
            var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, leaveOpen: true);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(records);
            await writer.FlushAsync();
            stream.Position = 0;
            return stream;
        }

        #region "SingleQueue"
        public async Task<List<LoginingAgents_Datatable>> GetLogedinAgents(int queueNum, string queueName, QueueLogsSearchDto dto, int page, int pageSize, CancellationToken ct)
        {
            string logining_sql = "CALL Get_queue_logining_agents(@p0, @p1, @p2, @p3)";
            var logining_result = await _myDbContext.Set<LoginingAgents_SP>().FromSqlRaw(logining_sql, queueNum, queueName, dto.From, dto.To).AsNoTracking().ToListAsync(ct);
            return _mapper.Map<List<LoginingAgents_Datatable>>(logining_result);
        }

        public async Task<List<LoginingAgents_Datatable>> ExportQueueLogedinAgents(int queueNum, string queueName, QueueLogsSearchDto searchDto, CancellationToken ct)
        {
            var records = await GetLogedinAgents(queueNum, queueName, searchDto, 1, int.MaxValue, ct);
            if (records.Count == 0 || records is null)
                return null;

            var exportRows = new List<LoginingAgents_Datatable>();
            foreach (var agent in records)
            {
                var ticketDto = new AgentTicketDto
                {
                    AgentNumber = agent.AgentNumber,
                    AgentName = agent.AgentName,
                    From = searchDto.From,
                    To = searchDto.To,
                    QueueName = agent.QName,
                    QueueNum = int.Parse(agent.QNum)
                };
                agent.AgentNumber = agent.AgentNumber?.Split('/')[1];
                exportRows.Add(agent);

                var tickets = await GetLoginingAgentTicket(ticketDto, ct);

                DateTime? currentLogin = null;

                foreach (var ev in tickets.OrderBy(x => x.EventTime))
                {
                    if (ev.EventType == "ADDMEMBER")
                    {
                        currentLogin = ev.EventTime;
                    }
                    else if (ev.EventType == "REMOVEMEMBER" && currentLogin.HasValue)
                    {
                        exportRows.Add(new LoginingAgents_Datatable
                        {
                            FirstLogin = currentLogin.Value.ToString("dd/MM/yyyy hh:mm:ss tt"),
                            LastLogout = ev.EventTime.ToString("dd/MM/yyyy hh:mm:ss tt"),
                            Duration = (ev.EventTime - currentLogin).ToString()
                        });
                        currentLogin = null;
                    }
                }
            }
            return exportRows;

        }
        // Done

        public async Task<List<PauseingAgents_Datatable>> GetPausedAgents(int queueNum, string queueName, QueueLogsSearchDto dto, int page, int pageSize, CancellationToken ct)
        {
            string pauseing_sql = "CALL Get_queue_paused_agents(@p0, @p1, @p2, @p3)";
            var pauseing_result = await _myDbContext.Set<PauseingAgents_SP>().FromSqlRaw(pauseing_sql, queueNum, queueName, dto.From, dto.To).AsNoTracking().ToListAsync(ct);
            return _mapper.Map<List<PauseingAgents_Datatable>>(pauseing_result);
        }

        public async Task<List<PauseingAgents_Datatable>> ExportQueuePausedAgents(int queueNum, string queueName, QueueLogsSearchDto searchDto, CancellationToken ct)
        {
            var records = await GetPausedAgents(queueNum, queueName, searchDto, 1, int.MaxValue, ct);
            if (records.Count == 0 || records is null)
                return null;

            var exportRows = new List<PauseingAgents_Datatable>();
            foreach (var agent in records)
            {
                var ticketDto = new AgentTicketDto
                {
                    AgentNumber = agent.AgentNumber,
                    AgentName = agent.AgentName,
                    From = searchDto.From,
                    To = searchDto.To,
                    QueueName = agent.QName,
                    QueueNum = int.Parse(agent.QNum)
                };
                agent.AgentNumber = agent.AgentNumber?.Split('/')[1];
                exportRows.Add(agent);

                var tickets = await GetPausedAgentTicket(ticketDto, ct);

                DateTime? currentPause = null;

                foreach (var ev in tickets.OrderBy(x => x.EventTime))
                {
                    if (ev.EventType == "PAUSE")
                    {
                        currentPause = ev.EventTime;
                    }
                    else if (ev.EventType == "UNPAUSE" && currentPause.HasValue)
                    {
                        exportRows.Add(new PauseingAgents_Datatable
                        {
                            FirstPause = currentPause.Value.ToString("dd/MM/yyyy hh:mm:ss tt"),
                            LastUnPause = ev.EventTime.ToString("dd/MM/yyyy hh:mm:ss tt"),
                            Duration = (ev.EventTime - currentPause).ToString()
                        });
                        currentPause = null;
                    }
                }
            }
            return exportRows;
        }
        // Done


        public async Task<Queue_charts_SP> GetQueueCharts(int queueNum, string queueName, QueueLogsSearchDto dto, CancellationToken ct)
        {
            string queue_charts_sql = "CALL Get_queue_detail_charts(@p0, @p1, @p2, @p3)";
            var result = await _myDbContext.Set<Queue_charts_SP>().FromSqlRaw(queue_charts_sql, queueNum, queueName, dto.From, dto.To).AsNoTracking().ToListAsync(ct);
            return result.FirstOrDefault();
        }

        public async Task<List<QueueDetails_Datatable>> GetQueueDetails(int queueNum, string queueName, QueueLogsSearchDto dto, QueueLogsAdvancedSearchDto advancedDto, int page, int pageSize, CancellationToken ct)
        {
            int skip = (page - 1) * pageSize;
            string queue_details_sql = "CALL Get_queue_details(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11)";
            _myDbContext.Database.SetCommandTimeout(1000); //10 minutes
            var calls_result = await _myDbContext.Set<QueueDetails_SP>()
            .FromSqlRaw(queue_details_sql,
             queueNum,
             queueName,
             dto.From,
             dto.To,
             pageSize,
             skip,
             advancedDto.Abandon ? 1 : 0,
             advancedDto.Answered ? 1 : 0,
             advancedDto.Agent ?? string.Empty,
             advancedDto.AgentName ?? string.Empty,
             advancedDto.HangUpBy ?? string.Empty,
             advancedDto.Caller ?? string.Empty).AsNoTracking().ToListAsync(ct);
            return _mapper.Map<List<QueueDetails_Datatable>>(calls_result);
        }
        // Done

        public async Task<List<AgentTicket_SP>> GetPausedAgentTicket(AgentTicketDto dto, CancellationToken ct)
        {
            string queue_charts_sql = "CALL Get_paused_agent_ticket(@p0, @p1, @p2, @p3, @p4, @p5)";
            return await _myDbContext.Set<AgentTicket_SP>().FromSqlRaw(queue_charts_sql, dto.QueueNum, dto.QueueName, dto.AgentNumber, dto.AgentName, dto.From, dto.To).AsNoTracking().ToListAsync(ct);
        }

        public async Task<List<AgentTicket_SP>> GetLoginingAgentTicket(AgentTicketDto dto, CancellationToken ct)
        {
            string queue_charts_sql = "CALL Get_logining_agent_ticket(@p0, @p1, @p2, @p3, @p4, @p5)";
            return await _myDbContext.Set<AgentTicket_SP>().FromSqlRaw(queue_charts_sql, dto.QueueNum, dto.QueueName, dto.AgentNumber, dto.AgentName, dto.From, dto.To).AsNoTracking().ToListAsync(ct);
        }
        // Done

        public async Task<List<CallDetails_Datatable>> GetQueueCallsDetail(int queueNum, string queueName, QueueLogsSearchDto dto, QueueLogsAdvancedSearchDto advancedDto, int page, int pageSize, CancellationToken ct)
        {
            string queue_call_detail_sql = "CALL Get_queue_call_details(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13)";
            int skip = (page - 1) * pageSize;
            _myDbContext.Database.SetCommandTimeout(1000); //10 minutes
            var calls_result = await _myDbContext.Set<CallDetails_SP>().FromSqlRaw(queue_call_detail_sql,
             queueNum,
             queueName,
             dto.From,
             dto.To,
             pageSize,
             skip,
             advancedDto.Abandon ? 1 : 0,
             advancedDto.Answered ? 1 : 0,
             advancedDto.NoAnswered ? 1 : 0,
             advancedDto.Canceled ? 1 : 0,
             advancedDto.Agent ?? string.Empty,
             advancedDto.AgentName ?? string.Empty,
             advancedDto.HangUpBy ?? string.Empty,
             advancedDto.Caller ?? string.Empty).AsNoTracking().ToListAsync(ct);
            return _mapper.Map<List<CallDetails_Datatable>>(calls_result);
        }

        public async Task<List<CallsPerHour_Datatable>> GetQueueCallsPerHour(int queueNum, string queueName, QueueLogsSearchDto dto, int page, int pageSize, CancellationToken ct)
        {
            string calls_per_hour_sql = "CALL Get_queue_calls_per_hour(@p0, @p1, @p2, @p3)";
            var result = await _myDbContext.Set<CallsPerHour_SP>().FromSqlRaw(calls_per_hour_sql, queueNum, queueName, dto.From, dto.To).AsNoTracking().ToListAsync(ct);
            return [.. result.GroupBy(p => new { p.QNum, p.QName })
                           .Select(g => new CallsPerHour_Datatable
                           {
                               QNum = g.Key.QNum,
                               QName = g.Key.QName,
                               HourlyCallCounts = Enumerable.Range(0, 24)
                                        .ToDictionary(hour => $"H{hour:D2}".ToString(), hour => new CallsPerHour_Datatable.HourlyCallStats(){
                                            CallCount = g.FirstOrDefault(x => x.Hour == hour)?.CallCount,
                                            AnsweredCount = g.FirstOrDefault(x => x.Hour == hour)?.TotalAnswerd,
                                            AbandonedCount = g.FirstOrDefault(x => x.Hour == hour)?.TotalAbandoned
                                        })
                           })];

        }
        // Done
        public async Task<List<CallsPerMonth_Datatable>> GetQueueCallsPerMonth(int queueNum, string queueName, QueueLogsSearchDto dto, int page, int pageSize, CancellationToken ct)
        {
            string calls_per_month_sql = "CALL Get_queue_calls_per_month(@p0, @p1, @p2, @p3)";
            var result = await _myDbContext.Set<CallsPerMonth_SP>().FromSqlRaw(calls_per_month_sql, queueNum, queueName, dto.From, dto.To).AsNoTracking().ToListAsync(ct);
            return [.. result.GroupBy(p => new { p.QNum, p.QName })
                           .Select(g => new CallsPerMonth_Datatable
                           {
                               QNum = g.Key.QNum,
                               QName = g.Key.QName,
                               MonthlyCallCounts = Enumerable.Range(1, 12)
                                        .ToDictionary(month => $"M{month:D2}".ToString(), month => new CallsPerMonth_Datatable.MonthlyCallStats(){
                                            CallCount = g.FirstOrDefault(x => x.Month == month)?.CallCount,
                                            AnsweredCount = g.FirstOrDefault(x => x.Month == month)?.TotalAnswerd,
                                            AbandonedCount = g.FirstOrDefault(x => x.Month == month)?.TotalAbandoned
                                        })
                           })];
        }
        // Done

        public async Task<int> GetSingleQueueDetailsReportTotalCount(string command, int queueNum, string queueName, QueueLogsSearchDto dto, QueueLogsAdvancedSearchDto advancedDto, CancellationToken ct)
        {
            await using var conn = _myDbContext.Database.GetDbConnection();
            await conn.OpenAsync(ct);
            _myDbContext.Database.SetCommandTimeout(1000); //10 minutes
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = command;
            cmd.CommandType = CommandType.Text;

            cmd.Parameters.Add(new MySqlParameter("@queueNum", queueNum));
            cmd.Parameters.Add(new MySqlParameter("@queueName", queueName));
            cmd.Parameters.Add(new MySqlParameter("@from", dto.From));
            cmd.Parameters.Add(new MySqlParameter("@to", dto.To));
            cmd.Parameters.Add(new MySqlParameter("@abandon", advancedDto.Abandon ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@answered", advancedDto.Answered ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@agent", advancedDto.Agent ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@agent_name", advancedDto.AgentName ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@hang_up_by", advancedDto.HangUpBy ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@caller", advancedDto.Caller ?? string.Empty));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return Convert.ToInt32(reader["total_count"]);
            }
            return 0;

        }

        public async Task<int> GetSingleQueueCallDetailsReportTotalCount(string command, int queueNum, string queueName, QueueLogsSearchDto dto, QueueLogsAdvancedSearchDto advancedDto, CancellationToken ct)
        {
            await using var conn = _myDbContext.Database.GetDbConnection();
            await conn.OpenAsync(ct);
            _myDbContext.Database.SetCommandTimeout(1000); //10 minutes
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = command;
            cmd.CommandType = CommandType.Text;

            cmd.Parameters.Add(new MySqlParameter("@queueNum", queueNum));
            cmd.Parameters.Add(new MySqlParameter("@queueName", queueName));
            cmd.Parameters.Add(new MySqlParameter("@from", dto.From));
            cmd.Parameters.Add(new MySqlParameter("@to", dto.To));
            cmd.Parameters.Add(new MySqlParameter("@abandon", advancedDto.Abandon ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@answered", advancedDto.Answered ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@no_answered", advancedDto.NoAnswered ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@canceled", advancedDto.Canceled ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@agent", advancedDto.Agent ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@agent_name", advancedDto.AgentName ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@hang_up_by", advancedDto.HangUpBy ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@caller", advancedDto.Caller ?? string.Empty));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return Convert.ToInt32(reader["total_count"]);
            }
            return 0;

        }

        #endregion

        #region "GetAllQueues"

        public async Task<List<string?>> UserPortals(string userId)
        {
            //filter based on portal
            var roleId = await _myDbContext.UserRoles.Where(x => x.UserId == userId).Select(x => x.RoleId).FirstOrDefaultAsync();
            return await _myDbContext.QueuePortals.Where(x => x.RoleId == roleId).Select(x => x.QueuePortal).ToListAsync();
        }

        public async Task<string> GetUserRoleId(string userId)
        {
            var roleId = await _myDbContext.UserRoles.Where(x => x.UserId == userId).Select(x => x.RoleId).FirstOrDefaultAsync();
            return await _myDbContext.QueuePortals.Where(x => x.RoleId == roleId).Select(x => x.RoleId).FirstOrDefaultAsync();
        }
        public async Task<List<Statistics_Datatable>> GetMainQueues(QueueLogsSearchDto dto, string userId, CancellationToken ct)
        {
            //get portals
            var listOfPortal = await UserPortals(userId);

            //execute query then filter
            string queue_main_details_sql = "CALL Get_queues_statistics(@p0,@p1)";
            var query = await _myDbContext.Set<Statistics_SP>().FromSqlRaw(queue_main_details_sql, dto.From, dto.To).AsNoTracking().ToListAsync(ct);

            if (listOfPortal.Count == 0)
            {
                return _mapper.Map<List<Statistics_Datatable>>(query);
            }
            else
            {
                var filtered_result = query.Where(x => listOfPortal.Contains(x.QNum));
                return _mapper.Map<List<Statistics_Datatable>>(filtered_result);
            }
        }

        public async Task<Queue_charts_SP> GetQueuesCharts(QueueLogsSearchDto dto, string userId, CancellationToken ct)
        {

            string? roleId = await GetUserRoleId(userId);
            string queue_charts_sql = "CALL Get_all_queues_details_charts(@p0, @p1, @p2)";
            var result = await _myDbContext.Set<Queue_charts_SP>().FromSqlRaw(queue_charts_sql, dto.From, dto.To, roleId).AsNoTracking().ToListAsync(ct);
            return result.FirstOrDefault();
        }

        public async Task<List<QueueDetails_Datatable>> GetAllQueuesDetail(QueueLogsSearchDto dto, QueueLogsAdvancedSearchDto advancedDto, int page, int pageSize, string userId, CancellationToken ct)
        {
            int skip = (page - 1) * pageSize;
            string roleId = await GetUserRoleId(userId);
            string queues_details_sql = "CALL Get_all_queues_detail(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10)";
            _myDbContext.Database.SetCommandTimeout(1000); //10 minutes
            var calls_result = await _myDbContext.Set<QueueDetails_SP>().FromSqlRaw(queues_details_sql,
              dto.From,
              dto.To,
              pageSize,
              skip,
              advancedDto.Abandon ? 1 : 0,
              advancedDto.Answered ? 1 : 0,
              advancedDto.Agent ?? string.Empty,
              advancedDto.AgentName ?? string.Empty,
              advancedDto.HangUpBy ?? string.Empty,
              advancedDto.Caller ?? string.Empty,
              roleId)
              .AsNoTracking()
              .ToListAsync(ct);
            return _mapper.Map<List<QueueDetails_Datatable>>(calls_result);
        }

        public async Task<List<CallDetails_Datatable>> GetAllQueuesCallDetail(QueueLogsSearchDto dto, QueueLogsAdvancedSearchDto advancedDto, int page, int pageSize, string userId, CancellationToken ct)
        {
            int skip = (page - 1) * pageSize;
            string roleId = await GetUserRoleId(userId);
            string queues_call_detail_sql = "CALL Get_all_queues_call_details(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12)";
            _myDbContext.Database.SetCommandTimeout(1000); //10 minutes
            var calls_result = await _myDbContext.Set<CallDetails_SP>()
            .FromSqlRaw(queues_call_detail_sql,
              dto.From,
              dto.To,
              pageSize,
              skip,
              advancedDto.Abandon ? 1 : 0,
              advancedDto.Answered ? 1 : 0,
              advancedDto.NoAnswered ? 1 : 0,
              advancedDto.Canceled ? 1 : 0,
              advancedDto.Agent ?? string.Empty,
              advancedDto.AgentName ?? string.Empty,
              advancedDto.HangUpBy ?? string.Empty,
              advancedDto.Caller ?? string.Empty,
              roleId
              ).AsNoTracking().ToListAsync(ct);
            return _mapper.Map<List<CallDetails_Datatable>>(calls_result);
        }

        public async Task<List<PauseingAgents_Datatable>> GetAllQueuesPausedAgents(QueueLogsSearchDto dto, int page, int pageSize, string userId, CancellationToken ct)
        {
            //get portal
            var listOfPortal = await UserPortals(userId);
            string pauseing_sql = "CALL Get_all_queues_pauseing_agents(@p0, @p1)";
            var query = await _myDbContext.Set<PauseingAgents_SP>().FromSqlRaw(pauseing_sql, dto.From, dto.To).AsNoTracking().ToListAsync(ct);
            if (listOfPortal.Count == 0)
            {
                return _mapper.Map<List<PauseingAgents_Datatable>>(query);
            }
            else
            {
                var filtered_result = query.Where(x => listOfPortal.Contains(x.QNum)).ToList();
                return _mapper.Map<List<PauseingAgents_Datatable>>(filtered_result);
            }
        }

        public async Task<List<LoginingAgents_Datatable>> GetAllQueuesLogedinAgents(QueueLogsSearchDto dto, int page, int pageSize, string userId, CancellationToken ct)
        {
            //get portal
            var listOfPortal = await UserPortals(userId);

            string logining_sql = "CALL Get_all_queues_logining_agents(@p0, @p1)";
            var query = await _myDbContext.Set<LoginingAgents_SP>().FromSqlRaw(logining_sql, dto.From, dto.To).AsNoTracking().ToListAsync(ct);

            if (listOfPortal.Count == 0)
            {
                return _mapper.Map<List<LoginingAgents_Datatable>>(query);
            }
            else
            {
                var logining_result = query.Where(x => listOfPortal.Contains(x.QNum)).ToList();
                return _mapper.Map<List<LoginingAgents_Datatable>>(logining_result);
            }
        }

        public async Task<List<CallsPerHour_Datatable>> GetCallsPerHour(QueueLogsSearchDto dto, int page, int pageSize, string userId, CancellationToken ct)
        {
            //get portal
            var listOfPortal = await UserPortals(userId);

            string calls_per_hour_sql = "CALL Get_all_calls_per_hour(@p0, @p1)";
            var result = await _myDbContext.Set<CallsPerHour_SP>().FromSqlRaw(calls_per_hour_sql, dto.From, dto.To).AsNoTracking().ToListAsync(ct);

            var resultDto = new List<CallsPerHour_SP>();
            if (listOfPortal.Count == 0)
            {
                resultDto = result;
            }
            else
            {
                resultDto = [.. result.Where(x => listOfPortal.Contains(x.QNum))];
            }
            return [.. resultDto.GroupBy(p => new { p.QNum, p.QName })
                           .Select(g => new CallsPerHour_Datatable
                           {
                               QNum = g.Key.QNum,
                               QName = g.Key.QName,
                               HourlyCallCounts = Enumerable.Range(0, 24)
                                        .ToDictionary(hour => $"H{hour:D2}".ToString(), hour => new CallsPerHour_Datatable.HourlyCallStats(){
                                            CallCount = g.FirstOrDefault(x => x.Hour == hour)?.CallCount,
                                            AnsweredCount = g.FirstOrDefault(x => x.Hour == hour)?.TotalAnswerd,
                                            AbandonedCount = g.FirstOrDefault(x => x.Hour == hour)?.TotalAbandoned
                                        })
                           })];

        }

        public async Task<List<CallsPerMonth_Datatable>> GetCallsPerMonth(QueueLogsSearchDto dto, int page, int pageSize, string userId, CancellationToken ct)
        {
            //get portal
            var listOfPortal = await UserPortals(userId);

            string calls_per_month_sql = "CALL Get_all_calls_per_month(@p0, @p1)";
            var result = await _myDbContext.Set<CallsPerMonth_SP>().FromSqlRaw(calls_per_month_sql, dto.From, dto.To).AsNoTracking().ToListAsync(ct);

            var resultDto = new List<CallsPerMonth_SP>();
            if (listOfPortal.Count == 0)
            {
                resultDto = result;
            }
            else
            {
                resultDto = [.. result.Where(x => listOfPortal.Contains(x.QNum))];
            }
            return [.. resultDto.GroupBy(p => new { p.QNum, p.QName })
                           .Select(g => new CallsPerMonth_Datatable
                           {
                               QNum = g.Key.QNum,
                               QName = g.Key.QName,
                               MonthlyCallCounts = Enumerable.Range(1, 12)
                                        .ToDictionary(month => $"M{month:D2}".ToString(), month => new CallsPerMonth_Datatable.MonthlyCallStats(){
                                            CallCount = g.FirstOrDefault(x => x.Month == month)?.CallCount,
                                            AnsweredCount = g.FirstOrDefault(x => x.Month == month)?.TotalAnswerd,
                                            AbandonedCount = g.FirstOrDefault(x => x.Month == month)?.TotalAbandoned
                                        })
                           })];
        }

        public async Task<List<AgentTicket_SP>> GetAllQueuesPausedAgentTicket(AgentTicketDto dto, CancellationToken ct)
        {
            string queue_charts_sql = "CALL Get_all_queues_pauseing_agents_ticket(@p0, @p1, @p2, @p3, @p4, @p5)";
            return await _myDbContext.Set<AgentTicket_SP>().FromSqlRaw(queue_charts_sql, dto.QueueNum, dto.QueueName, dto.AgentNumber, dto.AgentName, dto.From, dto.To).AsNoTracking().ToListAsync(ct);
        }

        public async Task<List<PauseingAgents_Datatable>> ExportPausedAgentsWithSessions(QueueLogsSearchDto searchDto, string userId, CancellationToken ct)
        {
            var records = await GetAllQueuesPausedAgents(searchDto, 1, int.MaxValue, userId, ct);
            if (records.Count == 0 || records is null)
                return null;

            var exportRows = new List<PauseingAgents_Datatable>();
            foreach (var agent in records)
            {
                var ticketDto = new AgentTicketDto
                {
                    AgentNumber = agent.AgentNumber,
                    AgentName = agent.AgentName,
                    From = searchDto.From,
                    To = searchDto.To,
                    QueueName = agent.QName,
                    QueueNum = int.Parse(agent.QNum)
                };
                agent.AgentNumber = agent.AgentNumber?.Split('/')[1];
                exportRows.Add(agent);

                var tickets = await GetAllQueuesPausedAgentTicket(ticketDto, ct);

                DateTime? currentPause = null;

                foreach (var ev in tickets.OrderBy(x => x.EventTime))
                {
                    if (ev.EventType == "PAUSE")
                    {
                        currentPause = ev.EventTime;
                    }
                    else if (ev.EventType == "UNPAUSE" && currentPause.HasValue)
                    {
                        exportRows.Add(new PauseingAgents_Datatable
                        {
                            // AgentNumber = agent.AgentNumber,
                            // AgentName = agent.AgentName,
                            FirstPause = currentPause.Value.ToString("dd/MM/yyyy hh:mm:ss tt"),
                            LastUnPause = ev.EventTime.ToString("dd/MM/yyyy hh:mm:ss tt"),
                            // Duration = dur.HasValue ? dur.Value.ToString(@"hh:mm:ss") : "",
                            Duration = (ev.EventTime - currentPause).ToString(),
                            // TotalSessions = agent.TotalSessions,
                            // QName = agent.QName,
                            // QNum = agent.QNum
                        });
                        currentPause = null;
                    }
                }
            }
            return exportRows;
        }


        public async Task<List<AgentTicket_SP>> GetAllQueuesLoginingAgentTicket(AgentTicketDto dto, CancellationToken ct)
        {
            string queue_charts_sql = "CALL Get_all_queues_logining_agents_ticket(@p0, @p1, @p2, @p3, @p4, @p5)";
            return await _myDbContext.Set<AgentTicket_SP>().FromSqlRaw(queue_charts_sql, dto.QueueNum, dto.QueueName, dto.AgentNumber, dto.AgentName, dto.From, dto.To).AsNoTracking().ToListAsync(ct);
        }

        public async Task<List<LoginingAgents_Datatable>> ExportQueuesLoginingAgentTicket(QueueLogsSearchDto searchDto, string userId, CancellationToken ct)
        {
            var records = await GetAllQueuesLogedinAgents(searchDto, 1, int.MaxValue, userId, ct);
            if (records.Count == 0 || records is null)
                return null;

            var exportRows = new List<LoginingAgents_Datatable>();
            foreach (var agent in records)
            {
                var ticketDto = new AgentTicketDto
                {
                    AgentNumber = agent.AgentNumber,
                    AgentName = agent.AgentName,
                    From = searchDto.From,
                    To = searchDto.To,
                    QueueName = agent.QName,
                    QueueNum = int.Parse(agent.QNum)
                };
                agent.AgentNumber = agent.AgentNumber?.Split('/')[1];
                exportRows.Add(agent);

                var tickets = await GetAllQueuesLoginingAgentTicket(ticketDto, ct);

                DateTime? currentLogin = null;

                foreach (var ev in tickets.OrderBy(x => x.EventTime))
                {
                    if (ev.EventType == "ADDMEMBER")
                    {
                        currentLogin = ev.EventTime;
                    }
                    else if (ev.EventType == "REMOVEMEMBER" && currentLogin.HasValue)
                    {
                        exportRows.Add(new LoginingAgents_Datatable
                        {
                            // AgentNumber = agent.AgentNumber,
                            // AgentName = agent.AgentName,
                            FirstLogin = currentLogin.Value.ToString("dd/MM/yyyy hh:mm:ss tt"),
                            LastLogout = ev.EventTime.ToString("dd/MM/yyyy hh:mm:ss tt"),
                            // Duration = dur.HasValue ? dur.Value.ToString(@"hh:mm:ss") : "",
                            Duration = (ev.EventTime - currentLogin).ToString(),
                            // TotalSessions = agent.TotalSessions,
                            // QName = agent.QName,
                            // QNum = agent.QNum
                        });
                        currentLogin = null;
                    }
                }
            }
            return exportRows;
        }

        public async Task<DataToSearch> GetAllAgentNumAndNames(QueueLogsSearchDto dto, string userId, CancellationToken ct)
        {
            //get portals
            var listOfPortal = await UserPortals(userId);

            var query = _myDbContext.QueueLogs.Where(x => x.Time >= dto.From && x.Time <= dto.To);

            if (listOfPortal.Count != 0)
                query = query.Where(x => listOfPortal.Contains(x.QueueName));

            var agentNames = await query.Select(r => r.Data4).Distinct().AsNoTracking().ToListAsync(ct);
            var agentNumbers = await query.Select(r => r.Agent).Distinct().AsNoTracking().ToListAsync(ct);

            return new DataToSearch
            {
                AgentNames = agentNames,
                AgentNumbers = agentNumbers
            };
        }

        public async Task<DataToSearch> GetAllAgentNumAndNamesForSingleQueue(int queueNum, string queueName)
        {
            var query = _myDbContext.QueueLogs
            .Where(x => x.Data5 == queueName)
            .Where(x => x.QueueName == queueNum.ToString());

            var agentNames = await query.Select(r => r.Data4).Distinct().AsNoTracking().ToListAsync();
            var agentNumbers = await query.Select(r => r.Agent).Distinct().AsNoTracking().ToListAsync();

            return new DataToSearch
            {
                AgentNames = agentNames,
                AgentNumbers = agentNumbers
            };
        }

        public async Task<int> GetQueueDetailsReportTotalCount(string command, QueueLogsSearchDto dto, QueueLogsAdvancedSearchDto advancedDto, string userId, CancellationToken ct)
        {
            //get roleId
            string roleId = await GetUserRoleId(userId);
            await using var conn = _myDbContext.Database.GetDbConnection();
            await conn.OpenAsync(ct);
            _myDbContext.Database.SetCommandTimeout(1000); //10 minutes
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = command;
            cmd.CommandType = CommandType.Text;

            cmd.Parameters.Add(new MySqlParameter("@from", dto.From));
            cmd.Parameters.Add(new MySqlParameter("@to", dto.To));
            cmd.Parameters.Add(new MySqlParameter("@abandon", advancedDto.Abandon ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@answered", advancedDto.Answered ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@agent", advancedDto.Agent ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@agent_name", advancedDto.AgentName ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@hang_up_by", advancedDto.HangUpBy ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@caller", advancedDto.Caller ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@roleId", roleId));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return Convert.ToInt32(reader["total_count"]);
            }
            return 0;
        }

        public async Task<int> GetQueueCallsReportTotalCount(string command, QueueLogsSearchDto dto, QueueLogsAdvancedSearchDto advancedDto, string userId, CancellationToken ct)
        {
            //get roleId
            string roleId = await GetUserRoleId(userId);
            await using var conn = _myDbContext.Database.GetDbConnection();
            await conn.OpenAsync(ct);
            _myDbContext.Database.SetCommandTimeout(1000); //10 minutes
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = command;
            cmd.CommandType = CommandType.Text;

            cmd.Parameters.Add(new MySqlParameter("@from", dto.From));
            cmd.Parameters.Add(new MySqlParameter("@to", dto.To));
            cmd.Parameters.Add(new MySqlParameter("@abandon", advancedDto.Abandon ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@answered", advancedDto.Answered ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@no_answered", advancedDto.NoAnswered ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@canceled", advancedDto.Canceled ? 1 : 0));
            cmd.Parameters.Add(new MySqlParameter("@agent", advancedDto.Agent ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@agent_name", advancedDto.AgentName ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@hang_up_by", advancedDto.HangUpBy ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@caller", advancedDto.Caller ?? string.Empty));
            cmd.Parameters.Add(new MySqlParameter("@roleId", roleId));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return Convert.ToInt32(reader["total_count"]);
            }
            return 0;
        }

        public async Task<QueuesExamary> GetAllQueueExamary(QueueLogsSearchDto dto, string userId, CancellationToken ct)
        {
            //get portals (and where clause will be added if not empty)
            var listOfPortal = await UserPortals(userId);

            var query = _myDbContext.QueueLogs
                    .Where(x => x.Time >= dto.From && x.Time <= dto.To)
                    .Where(x => x.Event == "COMPLETEAGENT" || x.Event == "COMPLETECALLER" || x.Event == "ATTENDEDTRANSFER" || x.Event == "BLINDTRANSFER");

            if (listOfPortal.Count != 0)
            {
                query = query.Where(x => listOfPortal.Contains(x.QueueName));
            }
            var shortestCallBy = await query.OrderBy(x => Convert.ToInt32(x.Data2)).Select(x => new { x.Agent, x.Data4 }).FirstOrDefaultAsync(ct);

            var longestCallBy = await query.OrderByDescending(x => Convert.ToInt32(x.Data2)).Select(x => new { x.Agent, x.Data4 }).FirstOrDefaultAsync(ct);

            var listofPauseing = await GetAllQueuesPausedAgents(dto, 1, 10, userId, ct);

            var longestBreakBy = listofPauseing.OrderByDescending(x => x.DurationInSeconds)
            .Select(x => new { x.AgentName, x.AgentNumber, x.DurationInSeconds })
            .FirstOrDefault();

            return new QueuesExamary()
            {
                ShortestCallBy = shortestCallBy,
                LongestCallBy = longestCallBy,
                LongestBreakBy = longestBreakBy
            };
        }

        public async Task<QueuesExamary> GetQueueExamary(int queueNum, string queueName, QueueLogsSearchDto dto, CancellationToken ct)
        {
            var query = _myDbContext.QueueLogs
                    .Where(x => x.Time >= dto.From && x.Time <= dto.To)
                    .Where(x => x.QueueName == queueNum.ToString() && x.Data5 == queueName)
                    .Where(x => x.Event == "COMPLETEAGENT" || x.Event == "COMPLETECALLER" || x.Event == "ATTENDEDTRANSFER" || x.Event == "BLINDTRANSFER");

            var longestCallBy = await query
                    .OrderByDescending(x => Convert.ToInt32(x.Data2))
                    .Select(x => new { x.Agent, x.Data4 })
                    .FirstOrDefaultAsync();

            var shortestCallBy = await query
                   .OrderBy(x => Convert.ToInt32(x.Data2))
                   .Select(x => new { x.Agent, x.Data4 })
                   .FirstOrDefaultAsync();

            var MaxCallBy = await query
                    .Select(x => new { x.Agent, x.Data4 })
                    .GroupBy(x => new { x.Agent, x.Data4 })
                    .Select(g => new
                    {
                        Agent = g.Key.Agent,
                        AgentName = g.Key.Data4,
                        TotalCalls = g.Count()
                    })
                 .OrderByDescending(x => x.TotalCalls)
                 .FirstOrDefaultAsync();

            var listofPauseing = await GetPausedAgents(queueNum, queueName, dto, 1, 10, ct);

            var longestBreakBy = listofPauseing.OrderByDescending(x => x.DurationInSeconds)
            .Select(x => new { x.AgentName, x.AgentNumber, x.DurationInSeconds })
            .FirstOrDefault();

            return new QueuesExamary()
            {
                ShortestCallBy = shortestCallBy,
                LongestCallBy = longestCallBy,
                LongestBreakBy = longestBreakBy,
                MaxCallBy = MaxCallBy
            };
        }

        public async Task<object> GetAgentsStats(QueueLogsFilteredSearchDto dto, string userId, CancellationToken ct)
        {
            string Get_agents_statistics = "CALL Get_agents_statistics(@p0, @p1, @p2, @p3, @p4,@p5,@p6,@p7,@p8)";

            var roleId = await GetUserRoleId(userId);
            var data = await _myDbContext
                .Set<Agent_queue_stats_SP>()
                .FromSqlRaw(Get_agents_statistics, dto.From, dto.To, dto.PageSize, ((dto.Page - 1) * dto.PageSize), dto.AgentName, dto.Extension, dto.QueueNum, dto.QueueName, roleId)
                .AsNoTracking()
                .ToListAsync(ct);

            var result = data
                // .OrderByDescending(x => x.AnsweredCalls)
                .GroupBy(x => new { x.AgentExtension, x.AgentName })
                .Select(agentGroup => new Agent_queue_stats_SP_Datatable
                {
                    AgentExtension = agentGroup.Key.AgentExtension,
                    AgentName = agentGroup.Key.AgentName.Trim(),
                    Queues = agentGroup
                        .GroupBy(x => new { x.QueueNumber, x.QueueName })
                        .Select(queueGroup => new Agent_queue_stats_SP_Grouped
                        {
                            QueueNumber = queueGroup.Key.QueueNumber,
                            QueueName = queueGroup.Key.QueueName,
                            Stats = queueGroup.Select(x => new Agent_calls_stats
                            {
                                AnsweredCalls = x.AnsweredCalls,
                                NoAnsweredCalls = x.NoAnsweredCalls,
                                UniqueCalls = x.UniqueCalls,
                                AnswerRate = x.AnswerRate,
                                TotalTalkTime = x.TotalTalkTime,
                                AvgTalkTime = x.AvgTalkTime,
                                LongestTalkTime = x.LongestTalkTime,
                                LongestCallId = x.LongestCallId,
                                LongestCallAt = x.LongestCallTime?.ToString("dd/MM/yyyy hh:mm:ss tt") ?? "",
                                ShortestTalkTime = x.ShortestTalkTime,
                                ShortestCallId = x.ShortestCallId,
                                ShortestCallAt = x.ShortestCallTime?.ToString("dd/MM/yyyy hh:mm:ss tt") ?? "",
                                Login_Duration = x.Login_Duration,
                                TotalSessions = x.TotalSessions
                            }).FirstOrDefault()
                        })
                        .OrderByDescending(x => x.Stats.AnsweredCalls)
                        .ToList()
                })
                .ToList();
            return new { Count = data.FirstOrDefault()?.Count ?? 0, result };
        }


        public async Task<List<CallDetails_Datatable>> GetCallsDetailByCallId(string callId, CancellationToken ct)
        {
            string queue_call_detail_sql = "CALL Get_call_details_By_Id(@p0)";
            var calls_result = await _myDbContext
                                        .Set<CallDetails_SP>()
                                        .FromSqlRaw(queue_call_detail_sql, callId)
                                        .AsNoTracking().ToListAsync(ct);
            return _mapper.Map<List<CallDetails_Datatable>>(calls_result);
        }

        #endregion
    }
}